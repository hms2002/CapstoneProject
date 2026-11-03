local out=assert(app.params.outputDir)
local report={}
local passed=0
local function check(ok,label)
  assert(ok,label);passed=passed+1;report[#report+1]='PASS '..label
end
local contact=Image(256,128,ColorMode.RGB)
for k,name in ipairs({'alcohol','fire'}) do
  local s=app.open(out..'/'..name..'.aseprite')
  check(s.width==64 and s.height==64,name..' native 64x64')
  check(#s.frames==4,name..' four frames')
  check(#s.layers==3,name..' three editable layers')
  check(#s.palettes[1]==(name=='alcohol' and 9 or 8),name..' palette has exactly two added colors plus transparency')
  check(#s.tags==1 and s.tags[1].name=='Idle',name..' Idle tag')
  check(#s.slices==1 and s.slices[1].pivot.x==32 and s.slices[1].pivot.y==32,name..' fixed ground-center pivot')
  local base=Image(64,64,ColorMode.RGB)
  local baseCel=s.layers[1]:cel(1);base:drawImage(baseCel.image,baseCel.position)
  for f=1,4 do
    check(math.abs(s.frames[f].duration-(name=='alcohol' and .16 or .10))<.00001,name..' duration '..f)
    local full=Image(64,64,ColorMode.RGB);full:drawSprite(s,f)
    local saved=Image{fromFile=out..'/'..name..'-frame-'..f..'.png'}
    check(full:isEqual(saved),name..' native source/export equality '..f)
    local ground=Image(64,64,ColorMode.RGB)
    local cel=s.layers[1]:cel(f);ground:drawImage(cel.image,cel.position)
    check(ground:isEqual(base),name..' static ground layer '..f)
    contact:drawImage(full,Point((f-1)*64,(k-1)*64))
  end
  s:close()
end
contact:resize(768,384)
contact:saveAs(out..'/all-frames-3x.png')
report[#report+1]='Total passed: '..passed
local file=assert(io.open(out..'/native-verification.txt','w'));file:write(table.concat(report,'\n')..'\n');file:close()
