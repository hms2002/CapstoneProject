-- Continue the user-selected revision2 artwork with65% ground opacity.
-- Preserve the flame-free ground byte-for-byte; strengthen independent particles only.
local src=assert(app.params.sourceDir)
local out=assert(app.params.outputDir)
local alcoholSrc=assert(app.params.alcoholDir)
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
local alcohol=app.open(alcoholSrc..'/alcohol.aseprite')
local fire=app.open(out..'/fire-ground.aseprite')
assert(#fire.layers==2 and #fire.frames==4 and fire.layers[1].opacity==166)
report[#report+1]='Ground sources/exports are unchanged copies of revision11; no ground pixels, layers, opacity or timing are edited.'

-- Small bending flame particles. Same three colors and8x8 canvas; maximum visible bounds5x6.
local colors={rgb(231,120,53),rgb(255,169,65),rgb(255,228,154)}
local shapes={
  {'..1..','.121.','.121.','..1..'},
  {'...1.','..11.','..121','.1231','.121.','..1..'},
  {'...1.','..11.','..121','.1231','12321','.121.'},
  {'.1...','.11..','.121.','1231.','.121.','..1..'},
  {'..1.','.121','.131','..1.'},
  {'.1.','121','.1.'},
  {'2','1'},
  {}
}
local particle=Sprite(8,8,ColorMode.RGB);particle.layers[1].name='Small flame particle only'
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
report[#report+1]='Particle:8x8/8x80ms/3 opaque colors;5x6 peak flame silhouette; grow, bend, shrink, disappear. Native source/PNG equality; final frame empty. Pivot4,6.'

-- Independent preview emissions. Movement is not baked into the ground source.
local emitters={{17,25,0,-1},{30,18,40,1},{43,25,80,-1},{22,34,120,1},
  {38,35,160,1},{47,37,200,-1},{30,45,240,-1},{18,43,280,1},
  {40,45,320,-1},{15,33,360,1},{33,29,400,-1},{26,25,440,1},
  {44,31,480,1},{24,46,520,-1},{35,40,560,1},{31,35,600,-1},
  {24,39,640,1},{37,26,680,-1},{20,31,720,-1},{40,41,760,1}}
local mirroredFrames={}
for f,a in ipairs(particleFrames) do
  local b=blank(8,8)
  for y=0,7 do for x=0,7 do local c=a:getPixel(x,y)
    if pc.rgbaA(c)>0 then assert(x>0);b:drawPixel(8-x,y,c) end
  end end
  mirroredFrames[f]=b
end
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
      a:drawImage(p[4]<0 and mirroredFrames[step] or particleFrames[step],Point(x-4,y-6))
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
comparison:resize(608,320)
local before=app.open(src..'/comparison-preview.aseprite')
assert(#before.frames==#comparison.frames)
for f=1,#comparison.frames do
  local a,b=comparison.layers[1]:cel(f),before.layers[1]:cel(f)
  assert(a.image:isEqual(b.image) and a.position.x==b.position.x and a.position.y==b.position.y)
end
before:close()
report[#report+1]='All160 ground/alcohol/background preview cels exactly match revision11; only the particle layer changes.'
exactPalette(comparison)
comparison:saveAs(out..'/comparison-preview.aseprite');comparison:saveCopyAs(out..'/comparison-preview.gif')
Image(comparison):saveAs(out..'/comparison-preview.png')
isolated:resize(320,320);exactPalette(isolated)
isolated:saveAs(out..'/fire-with-particles-preview.aseprite');isolated:saveCopyAs(out..'/fire-with-particles-preview.gif')
Image(isolated):saveAs(out..'/fire-with-particles-preview.png')
report[#report+1]='Preview:20 staggered emitters (+25%),14 active particles; mirrored flame variants;800ms period,560ms visible life, sideways drift and14px rise.3200ms composition; ground and particles remain distinct editable layers. No Unity ParticleSystem changed.'
local log=assert(io.open(out..'/particle-motion.csv','w'));log:write(table.concat(motionLog,'\n')..'\n');log:close()
for _,s in ipairs({alcohol,fire,particle,comparison,isolated}) do s:close() end
local file=assert(io.open(out..'/verification.txt','w'));file:write(table.concat(report,'\n')..'\nAseprite '..tostring(app.version)..'\n');file:close()
