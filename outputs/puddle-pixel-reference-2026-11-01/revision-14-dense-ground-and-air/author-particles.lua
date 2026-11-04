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
report[#report+1]='Ground sources/exports are unchanged copies of revision13; no ground pixels, layers, opacity or timing are edited.'

-- Dense near-ground flame particles; broad7x6 silhouettes on8x8 canvases.
local colors={rgb(240,93,26),rgb(255,174,41),rgb(255,240,156)}
local shapes={
  {'..11...','..121..','.12321.','..121..'},
  {'...1...','..121..','.12321.','1233321','.12221.'},
  {'....1..','...121.','.112321','1233321','.123321','..1221.'},
  {'..1....','.121...','123211.','1233321','.123321','..1221.'},
  {'...1...','..121..','.12321.','1233321','.12221.'},
  {'..121..','.12321.','1232321','.12221.'},
  {'..121..','.12221.','..111..'},
  {}
}
local particle=Sprite(8,8,ColorMode.RGB);particle.layers[1].name='Near-ground flame particle'
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
  a:saveAs(out..'/ground-flame-particle-frame-'..f..'.png');particleSheet:drawImage(a,Point((f-1)*8,0))
end
local tag=particle:newTag(1,8);tag.name='GroundFlameLife'
local slice=particle:newSlice(Rectangle(0,0,8,8));slice.name='EmissionPoint';slice.pivot=Point(4,6)
particle:saveAs(out..'/ground-flame-particle.aseprite');particle:saveCopyAs(out..'/ground-flame-particle.gif')
particleSheet:saveAs(out..'/ground-flame-particle-sheet.png')
local pcheck=app.open(out..'/ground-flame-particle.aseprite')
assert(pcheck.width==8 and pcheck.height==8 and #pcheck.frames==8 and flat(pcheck,8):isEmpty())
for f=1,8 do assert(flat(pcheck,f):isEqual(particleFrames[f]) and pcheck.frames[f].duration==.08) end
pcheck:close()
report[#report+1]='Near-ground:8x8/8x80ms/3 opaque colors;7x6 peak silhouette; grow, bend, shrink, disappear. Native source/PNG equality; final frame empty. Pivot4,6.'

-- Intermittent larger flare: independent sprite, never baked into the puddle.
local flareShapes={
  {'...1...','..121..','..232..','.12321.','..121..'},
  {'....1..','...11..','...121.','..1231.','.12321.','.12321.','..121..'},
  {'.....1.','....11.','...121.','...121.','..12321','..12321','.123321','1233321','.12221.'},
  {'..1....','..11...','..121..','...121.','..1231.','..12321','.123321','1233321','.12321.','..121..'},
  {'....1..','...121.','..1231.','..12321','.12321.','..121..','...1...'},
  {'..1..','.121.','12321','.121.','..1..'},
  {'..2','.21','..1'},
  {}
}
local flare=Sprite(12,12,ColorMode.RGB);flare.layers[1].name='Airborne flame particle';flare:setPalette(palette)
local flareFrames={};local flareSheet=blank(96,12)
for f,rows in ipairs(flareShapes) do
  if f>1 then flare:newEmptyFrame() end;flare.frames[f].duration=.08
  local a=blank(12,12)
  for y,row in ipairs(rows) do for x=1,#row do
    local index=tonumber(row:sub(x,x))
    if index then a:drawPixel(6+x-1-math.floor(#row/2),10+y-#rows,colors[index]) end
  end end
  cel(flare,flare.layers[1],f,a);flareFrames[f]=a
  a:saveAs(out..'/airborne-flame-particle-frame-'..f..'.png');flareSheet:drawImage(a,Point((f-1)*12,0))
end
local flareTag=flare:newTag(1,8);flareTag.name='AirborneLife'
local flareSlice=flare:newSlice(Rectangle(0,0,12,12));flareSlice.name='EmissionPoint';flareSlice.pivot=Point(6,10)
flare:saveAs(out..'/airborne-flame-particle.aseprite');flare:saveCopyAs(out..'/airborne-flame-particle.gif');flareSheet:saveAs(out..'/airborne-flame-particle-sheet.png')
local fc=app.open(out..'/airborne-flame-particle.aseprite')
assert(fc.width==12 and fc.height==12 and #fc.frames==8 and flat(fc,8):isEmpty())
for f=1,8 do assert(flat(fc,f):isEqual(flareFrames[f]) and fc.frames[f].duration==.08) end
fc:close()
report[#report+1]='Airborne:12x12/8x80ms/3 opaque colors;7x10 peak silhouette. Native source/PNG equality; final frame empty; pivot6,10.'

-- Two independent particle groups; the ground source remains untouched.
local groundMask=flat(fire,1)
local function inside(x,y)
  return x>=0 and y>=0 and x<64 and y<64 and pc.rgbaA(groundMask:getPixel(x,y))>0
end
local function mirrorFrames(frames,w)
  local result={}
  for f,a in ipairs(frames) do
    local b=blank(w,w)
    for y=0,w-1 do for x=0,w-1 do local c=a:getPixel(x,y)
      if pc.rgbaA(c)>0 then assert(x>0);b:drawPixel(w-x,y,c) end
    end end
    result[f]=b
  end
  return result
end
local mirroredGround=mirrorFrames(particleFrames,8)
local mirroredAir=mirrorFrames(flareFrames,12)
-- Farthest-point placement gives even coverage without rows of identical flames.
local candidates={}
for y=10,53,2 do for x=9,53,2 do
  if inside(x,y) and inside(x-1,y) and inside(x+1,y) and inside(x,y-1) and inside(x,y+1) then
    candidates[#candidates+1]={x,y,d=math.huge}
  end
end end
local nearEmitters={};local last={31,32}
for i=1,64 do
  local best,score=nil,-1
  for _,p in ipairs(candidates) do
    p.d=math.min(p.d,(p[1]-last[1])^2+(p[2]-last[2])^2)
    local value=p.d+((p[1]*17+p[2]*31)%11)*.001
    if not p.used and value>score then best,score=p,value end
  end
  assert(best);best.used=true;nearEmitters[i]={best[1],best[2]};last=best
end
local airEmitters={{18,23},{31,15},{44,24},{13,34},{24,31},{37,30},
  {49,35},{20,43},{32,45},{43,43},{29,37},{40,20}}
for _,list in ipairs({nearEmitters,airEmitters}) do for _,p in ipairs(list) do assert(inside(p[1],p[2])) end end
local motionLog={'time_ms,group,emitter,age_ms,x,y,flip,spawn_x,spawn_y,cycle'}
local function particlesAt(t,group)
  local a=blank(80,80)
  local near=group=='near'
  local list=near and nearEmitters or airEmitters
  local period=near and 640 or 800
  for i,p in ipairs(list) do
    local phase=near and ((i*170+30)%640) or math.floor((i-1)*800/12+.5)
    local age=(t+phase)%period
    local cycle=math.floor((t+phase)/period)%(near and 5 or 4)
    if age<560 then
      local step=math.floor(age/80)+1;local u=age/560
      local jitter=near and 3 or 5
      local sx=p[1]+((i+cycle*3)%jitter-math.floor(jitter/2))
      local sy=p[2]+((i*3+cycle)%jitter-math.floor(jitter/2))
      if not inside(sx,sy) then sx,sy=p[1],p[2] end
      local flip=(i+cycle)%2==0;local direction=flip and -1 or 1
      local drift,rise
      if near then
        drift=math.floor(math.sin(u*math.pi*2+i*.7)*.9+.5)
        rise=math.floor(2*u+.5)
      else
        drift=math.floor((sx-31)*u*.16+direction*(u*3+math.sin(u*math.pi*2+i*.7)*3)+.5)
        rise=math.floor(math.min(26,sy+3)*(u*.35+u*u*.65)+.5)
      end
      local x,y=8+sx+drift,8+sy-rise
      local frame=near and (flip and mirroredGround[step] or particleFrames[step]) or (flip and mirroredAir[step] or flareFrames[step])
      local px,py=near and 4 or 6,near and 6 or 10
      a:drawImage(frame,Point(x-px,y-py))
      -- x/y below use the same comparison-preview coordinates as prior revisions.
      motionLog[#motionLog+1]=table.concat({t,group,i,age,x+74,y,flip and 1 or 0,sx,sy,cycle},',')
    end
  end
  return a
end
local comparison=Sprite(152,80,ColorMode.RGB)
comparison.layers[1].name='01 Original floor + alcohol + unchanged ground'
local cNear=comparison:newLayer();cNear.name='02 Dense near-ground flames'
local cAir=comparison:newLayer();cAir.name='03 Airborne flames'
local isolated=Sprite(80,80,ColorMode.RGB)
isolated.layers[1].name='01 Review floor + unchanged ground'
local iNear=isolated:newLayer();iNear.name='02 Dense near-ground flames'
local iAir=isolated:newLayer();iAir.name='03 Airborne flames'
local groups=Sprite(240,80,ColorMode.RGB)
groups.layers[1].name='01 Review floor + unchanged ground'
local gNear=groups:newLayer();gNear.name='02 Near-ground - left and center'
local gAir=groups:newLayer();gAir.name='03 Airborne - center and right'
for f=1,160 do
  local t=(f-1)*20
  if f>1 then comparison:newEmptyFrame();isolated:newEmptyFrame() end
  comparison.frames[f].duration=.02;isolated.frames[f].duration=.02
  local near,air=particlesAt(t,'near'),particlesAt(t,'air')
  local a=floorImage(152,80,false,0,0);a:drawImage(floorImage(76,80,true,76,0),Point(76,0))
  a:drawImage(flat(alcohol,math.floor(t/160)%4+1),Point(6,8))
  local ground=flat(fire,math.floor(t/100)%4+1);a:drawImage(ground,Point(82,8))
  cel(comparison,comparison.layers[1],f,a)
  local cn,ca=blank(152,80),blank(152,80);cn:drawImage(near,Point(74,0));ca:drawImage(air,Point(74,0))
  cel(comparison,cNear,f,cn);cel(comparison,cAir,f,ca)
  local b=floorImage(80,80,true,80,0);b:drawImage(ground,Point(8,8))
  cel(isolated,isolated.layers[1],f,b);cel(isolated,iNear,f,near);cel(isolated,iAir,f,air)
  if f%2==1 then
    local g=(f+1)/2;if g>1 then groups:newEmptyFrame() end;groups.frames[g].duration=.04
    local bg=floorImage(240,80,true,80,0)
    for _,x in ipairs({8,88,168}) do bg:drawImage(ground,Point(x,8)) end
    for y=0,79 do bg:drawPixel(79,y,rgb(95,108,118));bg:drawPixel(159,y,rgb(95,108,118)) end
    local gn,ga=blank(240,80),blank(240,80)
    gn:drawImage(near,Point(0,0));gn:drawImage(near,Point(80,0))
    ga:drawImage(air,Point(80,0));ga:drawImage(air,Point(160,0))
    cel(groups,groups.layers[1],g,bg);cel(groups,gNear,g,gn);cel(groups,gAir,g,ga)
  end
end
-- Palette before integer scaling avoids scanning the same pixels16 times.
exactPalette(comparison);comparison:resize(608,320)
local before=app.open(src..'/comparison-preview.aseprite')
assert(#before.frames==#comparison.frames)
for f=1,#comparison.frames do
  local a,b=comparison.layers[1]:cel(f),before.layers[1]:cel(f)
  assert(a.image:isEqual(b.image) and a.position.x==b.position.x and a.position.y==b.position.y)
end
before:close()
report[#report+1]='All160 ground/alcohol/background preview cels exactly match revision13; only the two particle layers change.'
comparison:saveAs(out..'/comparison-preview.aseprite');comparison:saveCopyAs(out..'/comparison-preview.gif');Image(comparison):saveAs(out..'/comparison-preview.png')
exactPalette(isolated);isolated:resize(320,320)
isolated:saveAs(out..'/fire-with-particles-preview.aseprite');isolated:saveCopyAs(out..'/fire-with-particles-preview.gif');Image(isolated):saveAs(out..'/fire-with-particles-preview.png')
exactPalette(groups);groups:resize(960,320)
groups:saveAs(out..'/particle-groups-preview.aseprite');groups:saveCopyAs(out..'/particle-groups-preview.gif');Image(groups):saveAs(out..'/particle-groups-preview.png')
report[#report+1]='Two independent groups:64 near-ground emitters/640ms period/0-2px rise;12 airborne emitters/800ms period/up to26px rise. Both560ms visible life. Near and airborne art and preview layers are separate; ground unchanged; no Unity integration.'
local log=assert(io.open(out..'/particle-motion.csv','w'));log:write(table.concat(motionLog,'\n')..'\n');log:close()
for _,s in ipairs({alcohol,fire,particle,flare,comparison,isolated,groups}) do s:close() end
local file=assert(io.open(out..'/verification.txt','w'));file:write(table.concat(report,'\n')..'\nAseprite '..tostring(app.version)..'\n');file:close()
