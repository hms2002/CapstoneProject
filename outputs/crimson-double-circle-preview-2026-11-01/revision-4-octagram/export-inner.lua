local out=app.params.outputDir
local im=Image{fromFile=out..'/inner-preview.png'}
assert(im.colorMode==ColorMode.RGB)
local f=assert(io.open(out..'/inner.rgba','wb'))
f:write(im.bytes)
f:close()
print('inner '..im.width..'x'..im.height)
