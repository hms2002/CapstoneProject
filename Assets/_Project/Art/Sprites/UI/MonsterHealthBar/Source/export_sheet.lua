-- Run with Aseprite -b --script-param source=<layered .aseprite> --script-param output=<folder> --script <this file>.
-- Derived sheet only: source cel pixels are copied without drawing or recoloring.
local source=app.open(app.params.source)
local sheet=Image(64,32,ColorMode.RGB)
local entries={
 {name='04_frame',x=2,y=2},
 {name='01_track',x=2,y=12},
 {name='03_health_fill',x=2,y=18},
 {name='02_damage_trail',x=2,y=24}
}
for _,entry in ipairs(entries) do
 local found=false
 for _,layer in ipairs(source.layers) do if layer.name==entry.name then
  sheet:drawImage(layer:cel(1).image,Point(entry.x,entry.y));found=true
 end end
 assert(found,'Missing source part '..entry.name)
end
sheet:saveAs(app.params.output..'/MonsterHealthBar.png')
if app.params.referenceDir then
 local function part(name)
  for _,layer in ipairs(source.layers) do if layer.name==name then return layer:cel(1).image end end
 end
 local ratios={{1,1},{0.5,0.5},{0.35,0.75},{0,0}}
 for i,ratio in ipairs(ratios) do
  local im=Image(40,8,ColorMode.RGB)
  im:drawImage(part('01_track'),Point(2,2))
  for _,entry in ipairs({{'02_damage_trail',ratio[2]},{'03_health_fill',ratio[1]}}) do
   local p=part(entry[1])
   for y=0,3 do for x=0,math.floor(36*entry[2]+0.5)-1 do im:drawPixel(2+x,2+y,p:getPixel(x,y)) end end
  end
  im:drawImage(part('04_frame'))
  im:saveAs(app.params.referenceDir..'/reference-'..i..'.png')
 end
end
source:close()
