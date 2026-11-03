local out=app.params.outputDir
for _,p in ipairs({0,25,50,75,100}) do
  local f=assert(io.open(out..'/charge-'..p..'.rgba','rb'));local bytes=f:read('*a');f:close()
  local im=Image(320,220,ColorMode.RGB);im.bytes=bytes
  local s=Sprite(320,220,ColorMode.RGB);s.cels[1].image=im
  s:saveCopyAs(out..'/charge-'..p..'.png');s:close()
end
