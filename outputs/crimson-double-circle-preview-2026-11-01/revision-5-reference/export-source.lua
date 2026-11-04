local out=app.params.outputDir
local im=Image{fromFile=out..'/reference-preview.png'}
assert(im.colorMode==ColorMode.RGB)
local f=assert(io.open(out..'/reference.rgba','wb'))
f:write(im.bytes)
f:close()
print(im.width..'x'..im.height)
