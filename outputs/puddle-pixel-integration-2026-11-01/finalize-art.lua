local src=assert(app.params.sourceDir)
local out=assert(app.params.outputDir)
local pc=app.pixelColor
local function rgb(r,g,b) return pc.rgba(r,g,b,255) end
local function image(w,h) return Image(w,h,ColorMode.RGB) end
local function flat(s,f) local a=image(s.width,s.height);a:drawSprite(s,f);return a end
local function layer(s,l,f)
  local a=image(64,64);if s.layers[l] then local c=s.layers[l]:cel(f);if c then a:drawImage(c.image,c.position) end end;return a
end
local function cel(s,l,f,a)
  local c=l:cel(f);if c then s:deleteCel(c) end;s:newCel(l,f,a,Point(0,0))
end
local function export(s,name)
  local sheet=image(s.width*#s.frames,s.height)
  for f=1,#s.frames do local a=flat(s,f);sheet:drawImage(a,Point((f-1)*s.width,0));a:saveAs(out..'/'..name..'-'..f..'.png') end
  sheet:saveAs(out..'/'..name..'-sheet.png');s:saveAs(out..'/'..name..'.aseprite')
end
local beer=app.open(src..'/alcohol.aseprite')
local original=app.open(src..'/alcohol.aseprite')
local fire=app.open(src..'/fire-ground.aseprite')
local from={rgb(207,158,67),rgb(245,197,91),rgb(255,219,127),rgb(255,231,163),rgb(255,241,199),rgb(255,252,236)}
local to={rgb(183,123,53),rgb(215,155,70),rgb(234,180,92),rgb(245,205,123),rgb(249,225,160),rgb(255,243,206)}
local map={};for i,c in ipairs(from) do map[c]=to[i] end
for li=1,2 do for f=1,4 do
  local c=beer.layers[li]:cel(f);local a=Image(c.image)
  for p in a:pixels() do local value=p();if pc.rgbaA(value)>0 then p(assert(map[value])) end end
  c.image=a
end end
for f=1,4 do
  assert(layer(beer,3,f):isEqual(layer(original,3,f)))
  local a,b=flat(beer,f),flat(original,f)
  for y=0,63 do for x=0,63 do assert(pc.rgbaA(a:getPixel(x,y))==pc.rgbaA(b:getPixel(x,y))) end end
end
assert(beer.layers[1].opacity==166)
local colorReference=app.open(src..'/../revision-16-beer-bubbles-center-ignition/alcohol.aseprite')
for li=1,2 do for f=1,4 do assert(layer(beer,li,f):isEqual(layer(colorReference,li,f))) end end
colorReference:close()
local palette=Palette(10);palette:setColor(0,Color{r=0,g=0,b=0,a=0})
for i,c in ipairs(to) do palette:setColor(i,c) end
palette:setColor(7,pc.rgba(255,252,236,230));palette:setColor(8,pc.rgba(255,241,199,175));palette:setColor(9,pc.rgba(255,231,163,110))
beer:setPalette(palette)
export(beer,'alcohol');export(fire,'fire-ground')
local transition=app.open(src..'/alcohol-to-fire-center.aseprite')
local mask=flat(fire,1)
local function dist(x,y)
  local dx,dy=x-32,y-32;local theta=math.atan(dy,dx)
  return math.sqrt(dx*dx+dy*dy)*(1+.065*math.sin(theta*5+.4)+.035*math.sin(theta*9+1.2))
end
local max=0;for y=0,63 do for x=0,63 do if pc.rgbaA(mask:getPixel(x,y))>0 then max=math.max(max,dist(x,y)) end end end
for f=1,16 do
  local t=(f-1)*80;local p=(f-1)/15
  for li=1,3 do
    local a,b=layer(beer,li,math.floor(t/160)%4+1),layer(fire,li,math.floor(t/100)%4+1)
    for y=0,63 do for x=0,63 do if p>0 and (p>=1 or dist(x,y)<=max*p) then a:drawPixel(x,y,b:getPixel(x,y)) end end end
    cel(transition,transition.layers[li],f,a)
  end
end
assert(flat(transition,1):isEqual(flat(beer,1)))
assert(flat(transition,16):isEqual(flat(fire,1)))
export(transition,'center-ignition')
local bubbles=image(40,24)
for row,name in ipairs({'bubble-particle','bubble-particle-small','bubble-particle-soft'}) do
  local s=app.open(src..'/'..name..'.aseprite');export(s,name)
  for f=1,5 do bubbles:drawImage(flat(s,f),Point((f-1)*8,(row-1)*8)) end;s:close()
end
bubbles:saveAs(out..'/bubble-variants-sheet.png')
for _,name in ipairs({'ground-flame-particle','airborne-flame-particle'}) do
  local s=app.open(src..'/'..name..'.aseprite');export(s,name);s:close()
end
local note=assert(io.open(out..'/native-art-verification.txt','w'))
note:write('Aseprite '..tostring(app.version)..'\nRevision16 body/ripple RGB restored. Revision17 foam and bubbles retained. Every beer alpha pixel unchanged. Ground layer166/255. Ignition endpoints exact; radial maximum='..max..'\n');note:close()
for _,s in ipairs({beer,original,fire,transition}) do s:close() end
