local out=app.params.outputDir
local checks=0
for _,entry in ipairs({{'inner',128,128,'inner-preview.png'},{'outer',128,128,'outer-preview.png'},{'glyph-atlas',144,9,'glyph-atlas.png'}}) do
  local s=assert(app.open(out..'/'..entry[1]..'.aseprite'))
  assert(s.width==entry[2] and s.height==entry[3]);checks=checks+1
  assert(#s.frames==1);checks=checks+1
  local export=out..'/'..entry[1]..'-reopened.png'
  s:saveCopyAs(export);s:close()
  local actual=Image{fromFile=export}
  local expected=Image{fromFile=out..'/'..entry[4]}
  assert(actual.bytes==expected.bytes);checks=checks+1
end
local composite=Image{fromFile=out..'/combined-128.png'}
assert(composite.width==128 and composite.height==128);checks=checks+1
local f=assert(io.open(out..'/native-verification.txt','w'));f:write(checks..' native source/reopen/dimension/export checks passed\n');f:close()
print(checks..' native checks passed')
