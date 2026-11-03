local out=app.params.outputDir
for _,name in ipairs({'charge-0','charge-25','charge-50','charge-75','charge-100','layer-inner','layer-outer'}) do
  local f=assert(io.open(out..'/'..name..'.rgba','rb'));local bytes=f:read('*a');f:close()
  local im=Image(192,144,ColorMode.RGB);im.bytes=bytes
  local s=Sprite(192,144,ColorMode.RGB);s.cels[1].image=im
  s:saveCopyAs(out..'/'..name..'.png');s:close()
end
