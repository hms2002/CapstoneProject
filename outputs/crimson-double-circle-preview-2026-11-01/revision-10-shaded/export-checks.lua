local out=app.params.outputDir
for _,name in ipairs({'charge-0','charge-25','charge-50','charge-75','charge-100','layer-inner','layer-outer','charge-rotated'}) do
  local f=assert(io.open(out..'/'..name..'.rgba','rb'));local bytes=f:read('*a');f:close()
  local im=Image(240,180,ColorMode.RGB);im.bytes=bytes
  local s=Sprite(240,180,ColorMode.RGB);s.cels[1].image=im
  s:saveCopyAs(out..'/'..name..'.png');s:close()
end
local light=Image(240,180,ColorMode.RGB)
for y=0,179 do for x=0,239 do
  local row=math.floor(y/20)
  local edge=(x+(row%2)*10)%20==0 or y%20==0
  light:drawPixel(x,y,edge and app.pixelColor.rgba(80,85,89,255) or app.pixelColor.rgba(104,109,113,255))
end end
light:drawImage(Image{fromFile=out..'/outer-preview.png'},Point(60,30))
light:drawImage(Image{fromFile=out..'/inner-preview.png'},Point(80,50))
local s=Sprite(240,180,ColorMode.RGB);s.cels[1].image=light
s:saveCopyAs(out..'/charge-light.png');s:close()
