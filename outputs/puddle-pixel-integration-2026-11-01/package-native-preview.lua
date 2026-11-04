local dir=assert(app.params.outputDir)
local function make(prefix,count,name,hold)
  local s=Sprite(640,320,ColorMode.RGB)
  s.layers[1].name='Unity native prefab capture'
  for i=0,count-1 do
    if i>0 then s:newEmptyFrame() end
    local a=Image{fromFile=dir..'/native/'..prefix..string.format('%03d',i)..'.png'}
    s:newCel(s.layers[1],i+1,a,Point(0,0))
    s.frames[i+1].duration=.04
  end
  if hold then s.frames[1].duration=.6;s.frames[count].duration=.8 end
  app.sprite=s
  app.command.ColorQuantization{ui=false,maxColors=256,withAlpha=false}
  s:saveCopyAs(dir..'/'..name..'.gif')
  s:close()
  local verify=app.open(dir..'/'..name..'.gif')
  assert(verify.width==640 and verify.height==320 and #verify.frames==count)
  verify:close()
end
make('comparison-',80,'native-comparison',false)
make('ignition-',21,'native-center-ignition',true)
print('Native Unity previews packaged and reopened in Aseprite.')
