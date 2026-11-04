local out=assert(app.params.outputDir)
local pc=app.pixelColor
local passed,log=0,{}
local function check(ok,label) assert(ok,label);passed=passed+1;log[#log+1]='PASS '..label end
local function flat(s,f) local im=Image(s.width,s.height,ColorMode.RGB);im:drawSprite(s,f);return im end
for _,spec in ipairs({{'inner',128,128,5},{'outer',128,128,5},{'glyph-atlas',132,11,1}}) do
  local name,w,h,layers=table.unpack(spec)
  local s=assert(app.open(out..'/'..name..'.aseprite'))
  check(s.width==w and s.height==h,name..' dimensions')
  check(#s.frames==1 and #s.layers==layers,name..' editable source structure')
  check(flat(s,1).bytes==Image{fromFile=out..'/'..name..'.png'}.bytes,name..' PNG equality after native reopen')
  s:close()
end
local medallion=Image{fromFile=out..'/medallion.png'}
local oldMedallion=Image{fromFile=out..'/../revision-17-crimson-polish/medallion.png'}
check(medallion.width==10 and medallion.height==10 and medallion.bytes==oldMedallion.bytes,'Original 10px medallion frame restored exactly')
for _,name in ipairs({'inner','outer'}) do
  local a=Image{fromFile=out..'/'..name..'-base.png'}
  local b=Image{fromFile=out..'/../revision-18-oriented/'..name..'-base.png'}
  check(a.bytes==b.bytes,name..' concentric border shape/color preserved')
end
local inner=Image{fromFile=out..'/inner.png'}
for k=0,3 do
  local a=-math.pi/2+k*math.pi/2
  local x=math.floor(63.5+35*math.cos(a)-4.5+.5)
  local y=math.floor(63.5+35*math.sin(a)-4.5+.5)
  local empty=true
  for dy=3,6 do for dx=3,6 do if pc.rgbaA(inner:getPixel(x+dx,y+dy))~=0 then empty=false end end end
  check(empty,'Medallion '..k..' interior is transparent')
end
local animation=assert(app.open(out..'/rotation-native.aseprite'))
local gif=assert(app.open(out..'/opposing-rotation.gif'))
check(animation.width==160 and animation.height==160 and #animation.frames==120,'Native animation dimensions and 120 frames')
check(gif.width==480 and gif.height==480 and #gif.frames==120,'GIF dimensions and 120 frames')
local background=pc.rgba(28,23,26,255)
local initial=Image(160,160,ColorMode.RGB);initial:clear(background)
initial:drawImage(Image{fromFile=out..'/combined-128.png'},Point(16,16))
check(initial.bytes==flat(animation,1).bytes,'GIF composition starts at exact original PNG arrangement')
local last=nil
for f=1,120 do
  check(math.abs(animation.frames[f].duration-.08)<1e-6 and math.abs(gif.frames[f].duration-.08)<1e-6,'Native/GIF 80ms timing '..f)
  local source=flat(animation,f);local actual=flat(gif,f)
  local exact=true
  for y=0,479 do for x=0,479 do
    if source:getPixel(math.floor(x/3),math.floor(y/3))~=actual:getPixel(x,y) then exact=false;break end
  end;if not exact then break end end
  check(exact,'GIF exact palette and uniform 3x scale '..f)
  if last then check(last~=source.bytes,'Animation advances '..f) end
  last=source.bytes
end
-- Inspect rendered motion itself: the top empty medallion moves to the left
-- immediately after frame 1. The top outer F moves right while its top rotates.
local second=flat(animation,2)
local theta=math.pi*2/120
local mx=math.floor(63.5-35*math.sin(theta)-4.5+.5)+16
local my=math.floor(63.5-35*math.cos(theta)-4.5+.5)+16
local matched=true
for y=0,9 do for x=0,9 do
  local p=medallion:getPixel(x,y)
  if pc.rgbaA(p)>0 and second:getPixel(mx+x,my+y)~=p then matched=false end
end end
check(matched and mx<75,'Rendered inner medallion moves counterclockwise')
local outerGold=pc.rgba(255,198,90,255)
local ox=math.floor(63.5+55.5*math.sin(theta)+.5)+16
local oy=math.floor(63.5-55.5*math.cos(theta)+.5)+16
check(ox>80 and second:getPixel(ox-2,oy-4)==outerGold and second:getPixel(ox+2,oy-4)==outerGold,'Rendered outer F moves clockwise')
local contact=Sprite(640,160,ColorMode.RGB)
local strip=Image(640,160,ColorMode.RGB)
for i,f in ipairs({1,11,21,31}) do strip:drawImage(flat(animation,f),Point((i-1)*160,0)) end
contact.cels[1].image=strip;contact:resize(1280,320);contact:saveCopyAs(out..'/rotation-contact.png');contact:close()
animation:close();gif:close()
log[#log+1]='Total passed: '..passed
local file=assert(io.open(out..'/native-verification.txt','w'));file:write(table.concat(log,'\n')..'\n');file:close()
print(passed..' native/PNG/GIF checks passed')
