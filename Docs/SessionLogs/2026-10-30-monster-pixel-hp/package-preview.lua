local input=app.params.input
local output=app.params.output
local pc=app.pixelColor
local function c(r,g,b) return pc.rgba(r,g,b,255) end
local function rect(im,x,y,w,h,p)
 for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,p) end end
end
local fontFile=io.open(app.params.fontSource,'r');local text=fontFile:read('*a');fontFile:close()
local tableText=text:match('local font=(%b{})')
local font=assert(load('return '..tableText))()
local function label(im,text,x,y)
 for i=1,#text do
  local glyph=font[text:sub(i,i)]
  if glyph then for yy=1,5 do for xx=1,3 do if glyph[yy]:sub(xx,xx)=='1' then
   rect(im,x+(xx-1)*2,y+(yy-1)*2,2,2,c(216,213,222))
  end end end end
  x=x+8
 end
end
local board=Image(700,332,ColorMode.RGB)
rect(board,0,0,700,332,c(32,34,41))
local titles={'ORIGINAL','UNITY MESH','UNITY UI'}
local prefixes={'References/reference','mesh','images'}
for column=1,3 do
 local x=20+(column-1)*230
 label(board,titles[column],x,18)
 for state=1,4 do
  local im=Image{fromFile=input..'/'..prefixes[column]..'-'..state..'.png'}
  if column>1 then for y=0,im.height-1 do for xx=0,im.width-1 do
   local p=im:getPixel(xx,y)
   if pc.rgbaR(p)==0 and pc.rgbaG(p)==255 and pc.rgbaB(p)==255 then im:drawPixel(xx,y,0) end
  end end end
  im:resize(200,40)
  board:drawImage(im,Point(x,48+(state-1)*62))
 end
end
label(board,'FULL - HALF - DAMAGE TRAIL - EMPTY',20,296)
label(board,'NATIVE UNITY - 5X SOURCE PIXEL SCALE',20,314)
board:saveAs(output..'/native-comparison.png')
