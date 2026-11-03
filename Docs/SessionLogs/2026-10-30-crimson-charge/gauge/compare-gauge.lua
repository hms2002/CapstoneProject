local output=app.params.outputDir
local input=app.params.captureDir
local reference=app.open(app.params.reference)
local pc=app.pixelColor
local function col(r,g,b) return pc.rgba(r,g,b,255) end
local function rect(im,x,y,w,h,color)
 for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,color) end end
end
local function cel(name)
 for _,layer in ipairs(reference.layers) do
  if layer.name==name then return layer:cel(1) end
 end
 error('Missing reference layer '..name)
end
local function draw(im,name)
 local c=cel(name); im:drawImage(c.image,c.position)
end
local function original(ratio)
 local im=Image(32,10,ColorMode.RGB)
 draw(im,'01_trough')
 local name=ratio>=1 and '06_full_charge_fill_optional' or ratio>=0.5 and '04_orange_at_50_percent' or '02_fill_clip_26px'
 local c=cel(name)
 for y=3,6 do for x=3,2+math.floor(26*ratio+0.0001) do
  im:drawPixel(x,y,c.image:getPixel(x-c.position.x,y-c.position.y))
 end end
 draw(im,'03_frame')
 if ratio>=1 then draw(im,'05_full_charge_frame_optional') end
 return im
end
local function zoom(im,n)
 local copy=Image(im); copy:resize(im.width*n,im.height*n); return copy
end
local font={
 R={'110','101','110','101','101'},E={'111','100','110','100','111'},F={'111','100','110','100','100'},
 N={'101','111','111','111','101'},C={'111','100','100','100','111'},U={'101','101','101','101','111'},
 I={'111','010','010','010','111'},T={'111','010','010','010','010'},Y={'101','101','010','010','010'},
 ['0']={'111','101','101','101','111'},['2']={'110','001','010','100','111'},
 ['5']={'111','100','110','001','110'},['7']={'111','001','010','010','010'},['1']={'010','110','010','010','111'}
}
local function label(im,text,x,y)
 for i=1,#text do
  local glyph=font[text:sub(i,i)]
  if glyph then for yy=1,5 do for xx=1,3 do if glyph[yy]:sub(xx,xx)=='1' then
   rect(im,x+(xx-1)*2,y+(yy-1)*2,2,2,col(218,215,224))
  end end end end
  x=x+8
 end
end
local board=Image(608,370,ColorMode.RGB)
rect(board,0,0,608,370,col(41,46,48))
label(board,'REFERENCE',60,20); label(board,'UNITY',340,20)
local report={ 'Original layered Aseprite vs native Unity 32x10 gauge. RGB tolerance: 1 byte.',
 '25%/75% reference rounds fill width down; continuous uGUI fill is rasterized at pixel centers.' }
local ratios={0,0.25,0.5,0.75,1}
local labels={'0','25','50','75','100'}
local failed=false
for i,ratio in ipairs(ratios) do
 local expected=original(ratio)
 expected:saveAs(output..'/reference-'..(i-1)..'.png')
 local actual=Image{fromFile=input..'/gauge-'..(i-1)..'.png'}
 actual:saveAs(output..'/unity-gauge-'..(i-1)..'.png')
 local mismatch=0; local checked=0; local worst=0
 for y=0,9 do for x=0,31 do
  local p=expected:getPixel(x,y); local q=actual:getPixel(x,y)
  if pc.rgbaA(p)>0 then
   checked=checked+1
   local delta=math.max(math.abs(pc.rgbaR(p)-pc.rgbaR(q)),math.abs(pc.rgbaG(p)-pc.rgbaG(q)),math.abs(pc.rgbaB(p)-pc.rgbaB(q)))
   worst=math.max(worst,delta)
   if delta>1 then mismatch=mismatch+1 end
  end
 end end
 report[#report+1]=string.format('%d%%: %d opaque pixels, %d mismatched, max RGB error=%d',ratio*100,checked,mismatch,worst)
 if (ratio==0 or ratio==0.5 or ratio==1) and mismatch>0 then failed=true end
 label(board,labels[i],12,65+(i-1)*62)
 -- Composite both onto the same background to compare the silhouette without transparency differences.
 local renderedReference=Image(32,10,ColorMode.RGB)
 rect(renderedReference,0,0,32,10,col(41,46,48)); renderedReference:drawImage(expected)
 board:drawImage(zoom(renderedReference,7),Point(60,45+(i-1)*62))
 board:drawImage(zoom(actual,7),Point(340,45+(i-1)*62))
end
board:saveAs(output..'/gauge-comparison.png')
local file=io.open(output..'/gauge-comparison.txt','w'); file:write(table.concat(report,'\n')); file:close()
reference:close()
if failed then error('Native gauge differs from original: see gauge-comparison.txt') end
