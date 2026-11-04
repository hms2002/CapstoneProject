-- Continue the user-selected revision2 artwork with65% ground opacity.
-- Remove its painted flames; burning motion comes from independent pixel motes.
local src=assert(app.params.sourceDir)
local out=assert(app.params.outputDir)
local pc=app.pixelColor
local function rgb(r,g,b) return pc.rgba(r,g,b,255) end
local function blank(w,h) return Image(w,h,ColorMode.RGB) end
local function floorImage(w,h,right,ox,oy)
  local a=blank(w,h)
  for y=0,h-1 do for x=0,w-1 do
    local seam=((x+(ox or 0))%16==0 or (y+(oy or 0))%16==0)
    a:drawPixel(x,y,right and (seam and rgb(56,64,71) or rgb(65,74,80)) or (seam and rgb(36,43,48) or rgb(43,51,57)))
  end end
  return a
end
local function flat(s,f)
  local a=blank(s.width,s.height);a:drawSprite(s,f);return a
end
local function cel(s,layer,f,a)
  local old=layer:cel(f);if old then s:deleteCel(old) end
  s:newCel(layer,f,a,Point(0,0))
end
local function exactPalette(s)
  local seen,colors={},{}
  for f=1,#s.frames do
    local a=flat(s,f)
    for p in a:pixels() do local c=p();if not seen[c] then seen[c]=true;colors[#colors+1]=c end end
  end
  assert(#colors<256)
  local p=Palette(#colors);for i,c in ipairs(colors) do p:setColor(i-1,c) end;s:setPalette(p)
end
local report={}
local alcohol=app.open(src..'/alcohol.aseprite')
local fire=app.open(src..'/fire.aseprite')
assert(fire.layers[3].name=='03 Low flames' and fire.layers[1].opacity==166)
fire:deleteLayer(fire.layers[3])
fire:saveAs(out..'/fire-ground.aseprite')
local original=app.open(src..'/fire.aseprite')
local saved=app.open(out..'/fire-ground.aseprite')
assert(#saved.layers==2 and #saved.frames==4 and saved.layers[1].opacity==166)
for i=1,2 do
  assert(saved.layers[i].opacity==original.layers[i].opacity and saved.layers[i].name==original.layers[i].name)
  for f=1,4 do
    local a,b=saved.layers[i]:cel(f),original.layers[i]:cel(f)
    assert(a.image:isEqual(b.image) and a.position.x==b.position.x and a.position.y==b.position.y and a.opacity==b.opacity)
    assert(saved.frames[f].duration==original.frames[f].duration)
  end
end
assert(saved.tags[1].name=='Idle' and saved.slices[1].pivot.x==32 and saved.slices[1].pivot.y==32)
original.layers[3].isVisible=false
for f=1,4 do assert(flat(saved,f):isEqual(flat(original,f))) end
report[#report+1]='Ground: only the Low flames layer removed; all8 remaining cel images/positions/opacities, layer opacities, timings, tag and pivot preserved. Exact equality to original with flame layer disabled.'
original:close();saved:close()
local sheet=blank(256,64);local groundReview=Sprite(64,64,ColorMode.RGB)
for f=1,4 do
  local a=flat(fire,f);a:saveAs(out..'/fire-ground-frame-'..f..'.png');sheet:drawImage(a,Point((f-1)*64,0))
  if f>1 then groundReview:newEmptyFrame() end;groundReview.frames[f].duration=.1
  local b=floorImage(64,64,true,82,8);b:drawImage(a);cel(groundReview,groundReview.layers[1],f,b)
end
sheet:saveAs(out..'/fire-ground-sheet.png');exactPalette(groundReview);groundReview:saveAs(out..'/fire-ground.gif');groundReview:close()

-- Tiny square fragments, not flame-shaped silhouettes. Three original fire colors.
local colors={rgb(231,120,53),rgb(255,169,65),rgb(255,228,154)}
local shapes={
  {'11','12'},
  {'121','233','121'},
  {'123','233','121'},
  {'23','32','11'},
  {'23','12'},
  {'2','1'},
  {'2'},
  {}
}
local particle=Sprite(8,8,ColorMode.RGB);particle.layers[1].name='Pixel particle only'
local palette=Palette(4);palette:setColor(0,Color{r=0,g=0,b=0,a=0})
for i,c in ipairs(colors) do palette:setColor(i,c) end;particle:setPalette(palette)
local particleFrames={};local particleSheet=blank(64,8)
for f,rows in ipairs(shapes) do
  if f>1 then particle:newEmptyFrame() end;particle.frames[f].duration=.08
  local a=blank(8,8)
  for y,row in ipairs(rows) do for x=1,#row do
    local index=tonumber(row:sub(x,x))
    if index then a:drawPixel(4+x-1-math.floor(#row/2),6+y-#rows,colors[index]) end
  end end
  cel(particle,particle.layers[1],f,a);particleFrames[f]=a
  a:saveAs(out..'/pixel-particle-frame-'..f..'.png');particleSheet:drawImage(a,Point((f-1)*8,0))
end
local tag=particle:newTag(1,8);tag.name='ParticleLife'
local slice=particle:newSlice(Rectangle(0,0,8,8));slice.name='EmissionPoint';slice.pivot=Point(4,6)
particle:saveAs(out..'/pixel-particle.aseprite');particle:saveCopyAs(out..'/pixel-particle.gif')
particleSheet:saveAs(out..'/pixel-particle-sheet.png')
local pcheck=app.open(out..'/pixel-particle.aseprite')
assert(pcheck.width==8 and pcheck.height==8 and #pcheck.frames==8 and flat(pcheck,8):isEmpty())
for f=1,8 do assert(flat(pcheck,f):isEqual(particleFrames[f]) and pcheck.frames[f].duration==.08) end
pcheck:close()
report[#report+1]='Particle:8x8/8x80ms/3 opaque colors; grow, flicker, shrink, disappear. Native source/PNG equality; final frame empty. Pivot4,6.'

-- Independent preview emissions. Movement is not baked into the ground source.
local emitters={{17,25,0,-1},{30,18,50,1},{43,25,100,-1},{22,34,150,1},
  {38,35,200,1},{47,37,250,-1},{30,45,300,-1},{18,43,350,1},
  {40,45,400,-1},{15,33,450,1},{33,29,500,-1},{26,25,550,1},
  {44,31,600,1},{24,46,650,-1},{35,40,700,1},{31,35,750,-1}}
local motionLog={'time_ms,emitter,age_ms,x,y'}
local function particlesAt(t,w,h,ox,oy)
  local a=blank(w,h)
  for i,p in ipairs(emitters) do
    local age=(t+p[3])%800
    if age<560 then
      local step=math.floor(age/80)+1
      local u=age/560
      local drift=math.floor(p[4]*(u*3+math.sin(u*math.pi*2+i*.7)*2.5)+.5)
      local rise=math.floor(14*u+.5)
      local x,y=ox+p[1]+drift,oy+p[2]-rise
      a:drawImage(particleFrames[step],Point(x-4,y-6))
      if w==152 then motionLog[#motionLog+1]=table.concat({t,i,age,x,y},',') end
    end
  end
  return a
end
local comparison=Sprite(152,80,ColorMode.RGB)
comparison.layers[1].name='Original floor + alcohol + flame-free ground'
local particleLayer=comparison:newLayer();particleLayer.name='Independent pixel particles'
local isolated=Sprite(80,80,ColorMode.RGB)
isolated.layers[1].name='Review floor + flame-free ground'
local isolatedParticles=isolated:newLayer();isolatedParticles.name='Independent pixel particles'
for f=1,160 do
  local t=(f-1)*20
  if f>1 then comparison:newEmptyFrame();isolated:newEmptyFrame() end
  comparison.frames[f].duration=.02;isolated.frames[f].duration=.02
  local a=floorImage(152,80,false,0,0);a:drawImage(floorImage(76,80,true,76,0),Point(76,0))
  a:drawImage(flat(alcohol,math.floor(t/160)%4+1),Point(6,8))
  local ground=flat(fire,math.floor(t/100)%4+1);a:drawImage(ground,Point(82,8))
  cel(comparison,comparison.layers[1],f,a);cel(comparison,particleLayer,f,particlesAt(t,152,80,82,8))
  local b=floorImage(80,80,true,80,0);b:drawImage(ground,Point(8,8))
  cel(isolated,isolated.layers[1],f,b);cel(isolated,isolatedParticles,f,particlesAt(t,80,80,8,8))
end
comparison:resize(608,320);exactPalette(comparison)
comparison:saveAs(out..'/comparison-preview.aseprite');comparison:saveCopyAs(out..'/comparison-preview.gif')
Image(comparison):saveAs(out..'/comparison-preview.png')
isolated:resize(320,320);exactPalette(isolated)
isolated:saveAs(out..'/fire-with-particles-preview.aseprite');isolated:saveCopyAs(out..'/fire-with-particles-preview.gif')
Image(isolated):saveAs(out..'/fire-with-particles-preview.png')
report[#report+1]='Preview:16 staggered emitters,800ms period,560ms visible life, sideways drift and14px rise.3200ms composition; ground and particles remain distinct editable layers. No Unity ParticleSystem changed.'
local log=assert(io.open(out..'/particle-motion.csv','w'));log:write(table.concat(motionLog,'\n')..'\n');log:close()
for _,s in ipairs({alcohol,fire,particle,comparison,isolated}) do s:close() end
local file=assert(io.open(out..'/verification.txt','w'));file:write(table.concat(report,'\n')..'\nAseprite '..tostring(app.version)..'\n');file:close()
