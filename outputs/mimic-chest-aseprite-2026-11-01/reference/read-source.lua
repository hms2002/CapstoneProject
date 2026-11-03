-- Read-only source inspection performed inside Aseprite.
local root=assert(app.params.projectDir)
local out=assert(app.params.outputDir)
local pc=app.pixelColor
local log=assert(io.open(out..'/reference/source-measurements.txt','w'))
local function print(s) log:write(s..'\n') end
local world=Image{fromFile=root..'/Assets/_Project/Art/Sprites/Map/Environment/Level/All Chest Sheet.png'}
local ui=Image{fromFile=root..'/Assets/_Project/Art/Sprites/UI/Chest/examples.png'}
local function hex(p) return string.format('%02X%02X%02X',pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)) end
local function inspect(im,name)
 local counts={}
 for it in im:pixels() do local p=it();if pc.rgbaA(p)>0 then local h=hex(p);counts[h]=(counts[h] or 0)+1 end end
 local list={};for h,n in pairs(counts) do list[#list+1]={h,n} end
 table.sort(list,function(a,b)return a[2]>b[2] end)
 print(name..' '..im.width..'x'..im.height)
 for _,v in ipairs(list) do print(v[1]..' '..v[2]) end
 local enlarged=Image(im);enlarged:resize(im.width*8,im.height*8)
 enlarged:saveAs(out..'/reference/'..name..'-8x.png')
 im:saveAs(out..'/reference/'..name..'.png')
end
inspect(Image(world,Rectangle(0,0,128,16)),'world-reference')
inspect(Image(ui,Rectangle(384,20,80,122)),'ui-reference')
print('WORLD CLOSED PIXELS')
for y=0,15 do local row={};for x=0,15 do local p=world:getPixel(x,y);row[#row+1]=pc.rgbaA(p)==0 and '......' or hex(p) end;print(table.concat(row,' ')) end
log:close()
