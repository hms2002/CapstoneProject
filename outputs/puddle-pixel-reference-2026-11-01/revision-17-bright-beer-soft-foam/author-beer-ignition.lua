-- Native Aseprite authoring. Existing game/earlier source files stay read-only.
local out=assert(app.params.outputDir)
local src=assert(app.params.sourceDir)
local alcoholSrc=assert(app.params.alcoholDir)
local prior=assert(app.params.priorDir)
local pc=app.pixelColor
local function rgb(r,g,b,a) return pc.rgba(r,g,b,a or 255) end
local function blank(w,h) return Image(w,h,ColorMode.RGB) end
local function flat(s,f) local a=blank(s.width,s.height);a:drawSprite(s,f);return a end
local function cel(s,l,f,a)
  local old=l:cel(f);if old then s:deleteCel(old) end
  s:newCel(l,f,a,Point(0,0))
end
local function rawLayer(s,l,f)
  local a=blank(s.width,s.height)
  if s.layers[l] then local c=s.layers[l]:cel(f);if c then a:drawImage(c.image,c.position) end end
  return a
end
local function floorImage(w,h,right,ox,oy)
  local a=blank(w,h)
  for y=0,h-1 do for x=0,w-1 do
    local seam=((x+(ox or 0))%16==0 or (y+(oy or 0))%16==0)
    a:drawPixel(x,y,right and (seam and rgb(56,64,71) or rgb(65,74,80)) or (seam and rgb(36,43,48) or rgb(43,51,57)))
  end end
  return a
end
local function paletteFor(s)
  local seen,colors={},{}
  for f=1,#s.frames do for p in flat(s,f):pixels() do
    local c=p();if not seen[c] then seen[c]=true;colors[#colors+1]=c end
  end end
  -- Opaque review GIFs use an exact palette when possible.
  if #colors<=256 then
    local pal=Palette(#colors)
    for i,c in ipairs(colors) do pal:setColor(i-1,c) end
    s:setPalette(pal)
  end
end
local function pivot(s,x,y,name)
  local sl=s:newSlice(Rectangle(0,0,s.width,s.height));sl.name=name;sl.pivot=Point(x,y)
end
local function exportFrames(s,name)
  local sheet=blank(s.width*#s.frames,s.height)
  for f=1,#s.frames do local a=flat(s,f)
    a:saveAs(out..'/'..name..'-frame-'..f..'.png');sheet:drawImage(a,Point((f-1)*s.width,0))
  end
  sheet:saveAs(out..'/'..name..'-sheet.png')
  s:saveAs(out..'/'..name..'.aseprite')
end
local function savePreview(s,name,scale)
  paletteFor(s);s:resize(s.width*scale,s.height*scale)
  s:saveAs(out..'/'..name..'.aseprite');s:saveCopyAs(out..'/'..name..'.gif')
  flat(s,1):saveAs(out..'/'..name..'.png')
end
local report={}
local oldAlcohol=app.open(alcoholSrc..'/alcohol.aseprite')
local beer=app.open(alcoholSrc..'/alcohol.aseprite')
local fire=app.open(out..'/fire-ground.aseprite')
local oldColors={rgb(183,123,53),rgb(215,155,70),rgb(234,180,92),rgb(245,205,123),rgb(249,225,160),rgb(255,243,206)}
local newColors={rgb(207,158,67),rgb(245,197,91),rgb(255,219,127),rgb(255,231,163),rgb(255,241,199),rgb(255,252,236)}
local remap={};for i,c in ipairs(oldColors) do remap[c]=newColors[i] end
assert(#beer.layers==3 and #beer.frames==4 and beer.layers[1].opacity==166)
for li,l in ipairs(beer.layers) do
  assert(l.opacity==oldAlcohol.layers[li].opacity)
  for f=1,4 do
    local c=l:cel(f);local orig=oldAlcohol.layers[li]:cel(f)
    assert(c and orig and c.opacity==orig.opacity)
    local changed=Image(orig.image)
    for p in changed:pixels() do local value=p();if pc.rgbaA(value)>0 then p(assert(remap[value],'Unexpected beer color')) end end
    c.image=changed
    assert(c.position.x==orig.position.x and c.position.y==orig.position.y)
    assert(beer.frames[f].duration==oldAlcohol.frames[f].duration)
  end
end
-- Repaint only the foam layer: uneven little rafts and open surface bubbles.
-- The body and ripple masks, opacity and animation remain untouched.
local surfaceFoamColors={rgb(255,252,236,230),rgb(255,241,199,175),rgb(255,231,163,110)}
local function stamp(a,rows,ox,oy,colors)
  for y,row in ipairs(rows) do for x=1,#row do
    local v=tonumber(row:sub(x,x));if v then a:drawPixel(ox+x-1,oy+y-1,colors[v]) end
  end end
end
local surfaceBubbles={
  {'.1.','2..'},
  {'.12','2..','.3.'},
  {'.12.','2..3','.3..'},
  {'2...','...3'}
}
for f=1,4 do
  local a=blank(64,64)
  stamp(a,{'..11...','.122.2.','23...3.'},14,20,surfaceFoamColors)
  stamp(a,{'.1.','223'},41,15,surfaceFoamColors)
  stamp(a,{'1.2.','.223'},46,39,surfaceFoamColors)
  stamp(a,{'.12..','23.2.'},19,46,surfaceFoamColors)
  stamp(a,surfaceBubbles[f],24,32,surfaceFoamColors)
  stamp(a,surfaceBubbles[(f+1)%4+1],38,27,surfaceFoamColors)
  -- A few changing outer flecks break up identical patches without whole-clump jitter.
  if f%2==0 then a:drawPixel(19,22,0);a:drawPixel(18,23,surfaceFoamColors[3]) end
  if f>2 then a:drawPixel(48,41,surfaceFoamColors[3]);a:drawPixel(47,40,0) end
  cel(beer,beer.layers[3],f,a)
end
local beerPalette=Palette(7);beerPalette:setColor(0,Color{r=0,g=0,b=0,a=0})
for i,c in ipairs(newColors) do beerPalette:setColor(i,c) end;beer:setPalette(beerPalette)
exportFrames(beer,'alcohol')
local bcheck=app.open(out..'/alcohol.aseprite')
for f=1,4 do
  local before,after=flat(oldAlcohol,f),flat(bcheck,f)
  assert(after:isEqual(flat(beer,f)))
  for y=0,63 do for x=0,63 do
    local a,b=before:getPixel(x,y),after:getPixel(x,y)
    assert((pc.rgbaA(a)>0)==(pc.rgbaA(b)>0),'Beer footprint changed')
  end end
  for li=1,2 do
    local beforeLayer,afterLayer=rawLayer(oldAlcohol,li,f),rawLayer(bcheck,li,f)
    assert(bcheck.layers[li].opacity==oldAlcohol.layers[li].opacity)
    for y=0,63 do for x=0,63 do
      local a,b=beforeLayer:getPixel(x,y),afterLayer:getPixel(x,y)
      assert(pc.rgbaA(a)==pc.rgbaA(b),'Body/ripple alpha changed')
      if pc.rgbaA(a)>0 then assert(b==remap[a],'Unexpected body color/shape change') end
    end end
    beforeLayer:saveAs(out..'/check-before-layer-'..li..'-frame-'..f..'.png')
    afterLayer:saveAs(out..'/check-after-layer-'..li..'-frame-'..f..'.png')
  end
end
bcheck:close()
report[#report+1]='Beer: body/ripple RGB brighter; exact body/ripple alpha masks, cel/layer opacity and timing preserved. Ground166/255. Only surface foam shapes/alpha are reauthored as soft uneven patches and open glints. Overall footprint unchanged. Native reopen/PNG equality passed.'

-- Keep the game bubble source for provenance. Repaint its uniform white ring
-- as small, asymmetric cream arcs with sparse highlights for natural foam.
local gameBubble=app.open(out..'/existing-game-bubble.aseprite')
assert(gameBubble.width==32 and gameBubble.height==32 and #gameBubble.frames==1)
local foamColors={rgb(255,252,238,235),rgb(250,235,197,190),rgb(235,211,158,110),rgb(255,247,220,125)}
local foamVariants={
  {name='bubble-particle',rows={
    {'12','3.'},
    {'.11.','2..2','.33.'},
    {'.11.','2..2','2...','.33.'},
    {'..1..','.2..2','2....','.33..'},
    {'4....','....3','..3..'}
  }},
  {name='bubble-particle-small',rows={
    {'1','3'},
    {'.1.','2.2','.3.'},
    {'.12','2..','.3.'},
    {'1..','..3','.3.'},
    {'4..','..3'}
  }},
  {name='bubble-particle-soft',rows={
    {'.12','2..','.3.'},
    {'.11.','2..2','...3','.33.'},
    {'..11.','.2..2','2....','2...3','.33..'},
    {'..1..','.2...','2...3','....3','.3...'},
    {'.4....','.....3','......','..3...'}
  }}
}
local bubbleSprites,bubbleFrames={},{}
for vi,v in ipairs(foamVariants) do
  local s=Sprite(8,8,ColorMode.RGB);s.layers[1].name='Soft foam - asymmetric rim and small white glint'
  bubbleFrames[vi]={}
  for f,rows in ipairs(v.rows) do
    if f>1 then s:newEmptyFrame() end;s.frames[f].duration=.08
    local a=blank(8,8);local ox=4-math.floor(#rows[1]/2);local oy=4-math.floor(#rows/2)
    for y,row in ipairs(rows) do for x=1,#row do
      local value=tonumber(row:sub(x,x))
      if value then a:drawPixel(ox+x-1,oy+y-1,foamColors[value]) end
    end end
    bubbleFrames[vi][f]=a;cel(s,s.layers[1],f,a)
  end
  local bt=s:newTag(1,5);bt.name='BubbleRisePop';pivot(s,4,4,'BubbleCenter')
  paletteFor(s);exportFrames(s,v.name);bubbleSprites[#bubbleSprites+1]=s
end
report[#report+1]='Foam: three8x8 variants with3-5px peak bodies, asymmetric open cream rims, sparse off-white glints, warm translucent lower edges and a gentle pop. All keep5x80ms life, about7 births/sec and2-3 active; no change to beer transparency.'

local groundMask=flat(fire,1)
local function inside(x,y) return x>=0 and y>=0 and x<64 and y<64 and pc.rgbaA(groundMask:getPixel(x,y))>0 end
local function distance(x,y)
  local dx,dy=x-32,y-32;local d=math.sqrt(dx*dx+dy*dy)
  local theta=math.atan(dy,dx)
  return d*(1+.065*math.sin(theta*5+.4)+.035*math.sin(theta*9+1.2))
end
local maxDistance=0
for y=0,63 do for x=0,63 do if inside(x,y) then maxDistance=math.max(maxDistance,distance(x,y)) end end end
local function burned(x,y,progress)
  return progress>0 and (progress>=1 or distance(x,y)<=maxDistance*progress)
end
local transition=Sprite(64,64,ColorMode.RGB)
transition.layers[1].name='01 Center-out ground';transition.layers[1].opacity=166
local surface=transition:newLayer();surface.name='02 Beer ripples to fire heat'
local foam=transition:newLayer();foam.name='03 Foam disappears behind ignition'
local coverage={'frame,time_ms,progress,burned_pixels'}
local previousMask={}
for f=1,16 do
  if f>1 then transition:newEmptyFrame() end;transition.frames[f].duration=.08
  local t=(f-1)*80;local progress=(f-1)/15
  local count=0
  for y=0,63 do for x=0,63 do
    local key=y*64+x;local value=inside(x,y) and burned(x,y,progress)
    if previousMask[key] then assert(value,'Ignition regressed') end
    previousMask[key]=value;if value then count=count+1 end
  end end
  coverage[#coverage+1]=table.concat({f,t,progress,count},',')
  for li=1,3 do
    local a=rawLayer(beer,li,math.floor(t/160)%4+1)
    local b=rawLayer(fire,li,math.floor(t/100)%4+1)
    for y=0,63 do for x=0,63 do if burned(x,y,progress) then a:drawPixel(x,y,b:getPixel(x,y)) end end end
    cel(transition,transition.layers[li],f,a)
  end
end
assert(flat(transition,1):isEqual(flat(beer,1)))
assert(flat(transition,16):isEqual(flat(fire,1)))
local tt=transition:newTag(1,16);tt.name='IgniteFromCenter';pivot(transition,32,32,'GroundCenter')
paletteFor(transition);exportFrames(transition,'alcohol-to-fire-center')
local cf=assert(io.open(out..'/ignition-coverage.csv','w'));cf:write(table.concat(coverage,'\n')..'\n');cf:close()
report[#report+1]='Center ignition:64x64/16x80ms/center32,32. Monotonic irregular radial mask. First frame equals bright beer; last equals unchanged fire ground. Ground opacity166 and pivot32,32 retained; no baked flame shapes.'

-- Replay the accepted revision15 particle trajectories and frames exactly.
local near=app.open(out..'/ground-flame-particle.aseprite')
local air=app.open(out..'/airborne-flame-particle.aseprite')
local fireFrames={near={},air={}}
local mirrored={near={},air={}}
for group,s in pairs({near=near,air=air}) do for f=1,#s.frames do
  local a=flat(s,f);fireFrames[group][f]=a
  local b=blank(s.width,s.height)
  for y=0,s.height-1 do for x=0,s.width-1 do local c=a:getPixel(x,y)
    if pc.rgbaA(c)>0 then assert(x>0);b:drawPixel(s.width-x,y,c) end
  end end
  mirrored[group][f]=b
end end
local motion={}
for line in io.lines(src..'/particle-motion.csv') do
  if line:sub(1,7)~='time_ms' then
    local row={};for value in line:gmatch('[^,]+') do row[#row+1]=tonumber(value) or value end
    local tick=row[1]/20+1;motion[tick]=motion[tick] or {};motion[tick][#motion[tick]+1]=row
  end
end
local function ignitionProgress(t)
  if t<1280 then return 0 end
  return math.min(1,math.floor((t-1280)/80)/15)
end
local function fireAt(t,group,igniting)
  local a=blank(80,80)
  for _,r in ipairs(motion[math.floor(t/20)%160+1]) do if r[2]==group then
    local show=true
    if igniting then
      -- Let each particle begin its own life only after its emitter catches fire.
      show=burned(r[8],r[9],ignitionProgress(t-r[4]))
    end
    if show then
      local step=math.floor(r[4]/80)+1
      local frame=r[7]==1 and mirrored[group][step] or fireFrames[group][step]
      local px,py=group=='near' and 4 or 6,group=='near' and 6 or 10
      a:drawImage(frame,Point(r[5]-74-px,r[6]-py))
    end
  end end
  if igniting and group=='near' then
    -- The first spark is always at the requested center, never on the edge.
    local age=t-1360
    if age>=0 and age<560 then
      a:drawImage(fireFrames.near[math.floor(age/80)+1],Point(8+32-4,8+32-6-math.floor(age/280)))
    end
  end
  return a
end
local bubbleEmitters={{23,35},{37,24},{28,46},{42,36},{20,23},{34,40},{43,28},{27,29},{34,17},{18,38},{38,46},{30,35},{45,39},{25,19},{21,43},{40,20},{35,31},{29,41},{17,29},{41,43},{31,23},{46,32}}
for _,p in ipairs(bubbleEmitters) do assert(inside(p[1],p[2])) end
local bubbleMotion={'time_ms,emitter,age_ms,x,y,spawn_x,spawn_y,variant'}
local function bubblesAt(t,igniting,log)
  local a=blank(80,80)
  for i,p in ipairs(bubbleEmitters) do
    local age=(t+math.floor((i-1)*3200/22))%3200
    if age<400 and (not igniting or not burned(p[1],p[2],ignitionProgress(t))) then
      local step=math.floor(age/80)+1;local u=age/400
      local x=8+p[1]+math.floor(math.sin(u*math.pi)*((i%2==0) and 1 or -1)+.5)
      local y=8+p[2]-math.floor(u*(6+i%3)+.5)
      local variant=({1,2,2,3,1,2,1})[(i-1)%7+1]
      a:drawImage(bubbleFrames[variant][step],Point(x-4,y-4))
      if log then bubbleMotion[#bubbleMotion+1]=table.concat({t,i,age,x,y,p[1],p[2],variant},',') end
    end
  end
  return a
end

local comparison=Sprite(152,80,ColorMode.RGB)
comparison.layers[1].name='01 Floor - brighter beer - unchanged fire ground'
local cb=comparison:newLayer();cb.name='02 Soft varied beer foam'
local cn=comparison:newLayer();cn.name='03 Accepted near-ground flames'
local ca=comparison:newLayer();ca.name='04 Accepted airborne flames'
local oldPreview=app.open(src..'/comparison-preview.aseprite');oldPreview:resize(152,80)
local beerPreview=Sprite(80,80,ColorMode.RGB)
beerPreview.layers[1].name='01 Floor and brighter beer'
local bb=beerPreview:newLayer();bb.name='02 Soft varied beer foam'
for f=1,160 do
  local t=(f-1)*20
  if f>1 then comparison:newEmptyFrame();beerPreview:newEmptyFrame() end
  comparison.frames[f].duration=.02;beerPreview.frames[f].duration=.02
  local a=floorImage(152,80,false);a:drawImage(floorImage(76,80,true,76,0),Point(76,0))
  local beerFrame=flat(beer,math.floor(t/160)%4+1)
  a:drawImage(beerFrame,Point(6,8));a:drawImage(flat(fire,math.floor(t/100)%4+1),Point(82,8))
  cel(comparison,comparison.layers[1],f,a)
  local b,n,r=blank(152,80),blank(152,80),blank(152,80)
  local bubbles=bubblesAt(t,false,true)
  b:drawImage(bubbles,Point(-2,0));n:drawImage(fireAt(t,'near',false),Point(74,0));r:drawImage(fireAt(t,'air',false),Point(74,0))
  cel(comparison,cb,f,b);cel(comparison,cn,f,n);cel(comparison,ca,f,r)
  local old,now=flat(oldPreview,f),flat(comparison,f)
  for y=0,79 do for x=76,151 do assert(old:getPixel(x,y)==now:getPixel(x,y),'Fire preview changed') end end
  local left=floorImage(80,80,false);left:drawImage(beerFrame,Point(8,8))
  cel(beerPreview,beerPreview.layers[1],f,left);cel(beerPreview,bb,f,bubbles)
end
oldPreview:close()
report[#report+1]='All160 steady-fire preview frames match revision15 pixel-for-pixel across the entire right panel. Near35 active; airborne8-9, original colors/motion retained.'
savePreview(comparison,'comparison-preview',4)
savePreview(beerPreview,'alcohol-with-bubbles-preview',4)

-- Same floor and integer scale for a direct previous/current beer comparison.
local previousBeerPreview=app.open(prior..'/alcohol-with-bubbles-preview.aseprite')
previousBeerPreview:resize(80,80);beerPreview:resize(80,80)
local beforeAfter=Sprite(160,80,ColorMode.RGB)
beforeAfter.layers[1].name='Left revision16 - Right brighter beer and soft varied foam'
for f=1,160 do
  if f>1 then beforeAfter:newEmptyFrame() end;beforeAfter.frames[f].duration=.02
  local a=blank(160,80);a:drawImage(flat(previousBeerPreview,f),Point(0,0));a:drawImage(flat(beerPreview,f),Point(80,0))
  cel(beforeAfter,beforeAfter.layers[1],f,a)
end
savePreview(beforeAfter,'beer-before-after-preview',4)
previousBeerPreview:close();beforeAfter:close()

local demo=Sprite(80,80,ColorMode.RGB)
demo.layers[1].name='01 Review floor'
local dg=demo:newLayer();dg.name='02 Beer to fire from center'
local db=demo:newLayer();db.name='03 Beer bubbles - extinguish behind front'
local dn=demo:newLayer();dn.name='04 Near-ground flames - ignite from center'
local da=demo:newLayer();da.name='05 Airborne flames - ignite from center'
local contact=blank(80*6,80)
local contactTimes={1120,1440,1680,1920,2240,3120}
for f=1,240 do
  local t=(f-1)*20
  if f>1 then demo:newEmptyFrame() end;demo.frames[f].duration=.02
  cel(demo,demo.layers[1],f,floorImage(80,80,true))
  local a=blank(80,80);local ground
  if t<1280 then ground=flat(beer,math.floor(t/160)%4+1)
  elseif t<2560 then ground=flat(transition,math.floor((t-1280)/80)+1)
  else ground=flat(fire,math.floor((t-1280)/100)%4+1) end
  a:drawImage(ground,Point(8,8));cel(demo,dg,f,a)
  cel(demo,db,f,bubblesAt(t,true,false));cel(demo,dn,f,fireAt(t,'near',true));cel(demo,da,f,fireAt(t,'air',true))
  for i,ct in ipairs(contactTimes) do if t==ct then contact:drawImage(flat(demo,f),Point((i-1)*80,0)) end end
end
local dt1=demo:newTag(1,64);dt1.name='BeerWithBubbles'
local dt2=demo:newTag(65,128);dt2.name='CenterIgnition'
local dt3=demo:newTag(129,240);dt3.name='FireHold'
contact:saveAs(out..'/center-ignition-stages-native.png')
local contactSprite=Sprite(contact.width,contact.height,ColorMode.RGB)
cel(contactSprite,contactSprite.layers[1],1,contact);contactSprite:resize(contact.width*3,contact.height*3)
flat(contactSprite,1):saveAs(out..'/center-ignition-stages.png');contactSprite:close()
savePreview(demo,'center-ignition-preview',4)
report[#report+1]='Center demonstration:4.8s,1.28s beer hold +1.28s ignition +2.24s fire hold. Bubbles stop within burned area; individual fire particles begin from already-burned origins. Five editable preview layers.'
local motionFile=assert(io.open(out..'/bubble-motion.csv','w'));motionFile:write(table.concat(bubbleMotion,'\n')..'\n');motionFile:close()
local file=assert(io.open(out..'/verification.txt','w'));file:write(table.concat(report,'\n')..'\nAseprite '..tostring(app.version)..'\n');file:close()
for _,s in ipairs(bubbleSprites) do s:close() end
for _,s in ipairs({oldAlcohol,beer,fire,gameBubble,transition,near,air,comparison,beerPreview,demo}) do s:close() end
