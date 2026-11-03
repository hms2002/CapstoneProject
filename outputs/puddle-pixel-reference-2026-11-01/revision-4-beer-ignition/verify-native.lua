local out=assert(app.params.outputDir)
local passed=0;local log={};local opened={}
local function check(ok,label) assert(ok,label);passed=passed+1;log[#log+1]='PASS '..label end
local specs={
  {'beer',64,4,4,160,{{'BeerIdle',1,4}}},
  {'fire-surface',64,6,4,80,{{'FireLoop',1,6}}},
  {'beer-to-fire',64,8,4,80,{{'Ignite',1,8}}},
  {'flame-particle',16,6,1,80,{{'ParticleLife',1,6}}},
  {'puddle-cycle',64,18,4,0,{{'BeerIdle',1,4},{'Ignite',5,12},{'FireLoop',13,18}}}
}
local function flat(s,f)
  local a=Image(s.width,s.height,ColorMode.RGB);a:drawSprite(s,f);return a
end
for _,spec in ipairs(specs) do
  local name,w,count,layers,duration,tags=table.unpack(spec)
  local s=app.open(out..'/'..name..'.aseprite');opened[name]=s
  check(s.width==w and s.height==w,name..' native dimensions')
  check(#s.frames==count,name..' frame count')
  check(#s.layers==layers,name..' editable layers')
  check(#s.tags==#tags,name..' tag count')
  for i,t in ipairs(tags) do
    local actual=s.tags[i]
    check(actual.name==t[1] and actual.fromFrame.frameNumber==t[2] and actual.toFrame.frameNumber==t[3],name..' tag '..t[1])
  end
  local pivot=s.slices[1].pivot
  check(pivot.x==(w==64 and 32 or 8) and pivot.y==(w==64 and 32 or 14),name..' fixed pivot')
  for f=1,count do
    local ms=duration==0 and (f<=4 and 160 or 80) or duration
    check(math.abs(s.frames[f].duration-ms/1000)<.00001,name..' frame duration '..f)
    check(flat(s,f):isEqual(Image{fromFile=out..'/'..name..'-frame-'..f..'.png'}),name..' PNG pixel equality '..f)
  end
end
check(flat(opened['beer-to-fire'],1):isEqual(flat(opened.beer,1)),'Ignite starts with exact beer frame')
check(flat(opened['beer-to-fire'],8):isEqual(flat(opened['fire-surface'],1)),'Ignite ends with exact FireLoop start')
for f=1,18 do
  local name,index
  if f<=4 then name='beer';index=f elseif f<=12 then name='beer-to-fire';index=f-4 else name='fire-surface';index=(f-12)%6+1 end
  check(flat(opened['puddle-cycle'],f):isEqual(flat(opened[name],index)),'Combined timeline source equality '..f)
end
check(flat(opened['flame-particle'],6):isEmpty(),'Particle lifetime ends fully transparent')
for _,s in pairs(opened) do s:close() end
log[#log+1]='Total passed: '..passed
local file=assert(io.open(out..'/native-verification.txt','w'));file:write(table.concat(log,'\n')..'\n');file:close()
