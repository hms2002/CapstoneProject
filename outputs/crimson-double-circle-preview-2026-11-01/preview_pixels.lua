local out=app.params.outputDir
if app.params.mode=='read' then
  for _,name in ipairs({'outer','inner'}) do
    local im=Image{fromFile=out..'/'..name..'-preview.png'}
    assert(im.colorMode==ColorMode.RGB)
    local f=assert(io.open(out..'/'..name..'.rgba','wb'));f:write(im.bytes);f:close()
    print(name..' '..im.width..'x'..im.height)
  end
else
  for i=0,2 do
    local f=assert(io.open(out..'/check-'..i..'.rgba','rb'));local bytes=f:read('*a');f:close()
    local im=Image(320,220,ColorMode.RGB);im.bytes=bytes
    local s=Sprite(320,220,ColorMode.RGB);s.cels[1].image=im
    s:saveCopyAs(out..'/check-'..i..'.png');s:close()
  end
end
