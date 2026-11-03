-- Preview only. Revision24 uses tangent circles/squares and yellow exclusively for runes.
local out=assert(app.params.outputDir)
local baseline=assert(app.params.baselineDir)
local pc=app.pixelColor
local colors={pc.rgba(182,61,40,255),pc.rgba(241,107,50,255),pc.rgba(255,198,90,255)}
local N,C,S=192,95.5,1.5
local tau=2*math.pi
-- Circumcircle -> large-square incircle -> small-square incircle.
local outerSigilRadius=29.5
local largeHalfSide=outerSigilRadius/math.sqrt(2)
local smallHalfSide=outerSigilRadius/2
local paths={
 {{{-2,4},{-2,-4},{2,-4}},{{-2,0},{1,0}}},
 {{{-2,4},{-2,-4},{2,-4},{2,0},{-2,0}},{{-1,0},{2,4}}},
 {{{-2,4},{-2,-4},{2,4},{2,-4}}},
 {{{-2,-4},{-2,-1},{0,1},{2,-1},{2,-4}},{{0,-4},{0,4}}},
 {{{-2,4},{-2,-4},{2,-4},{2,0},{-2,0}}},
 {{{2,-4},{-2,-4},{1,0},{-2,4},{2,4}}},
 {{{-2,-4},{0,0},{2,-4}},{{0,0},{0,4}}},
 {{{-2,-4},{-2,4}},{{2,-4},{2,4}},{{-2,0},{2,0}}},
 {{{-1,-1},{-1,1}},{{-1,0},{1,0}}},
 {{{-1,-1},{1,-1}},{{0,-1},{0,1}}},
 {{{-1,0},{1,0}},{{0,-1},{0,1}}},
 {{{-1,1},{-1,-1},{1,-1},{1,1}}}
}
local function image(w,h) return Image(w,h or w,ColorMode.RGB) end
local function put(im,x,y,color)
 x=math.floor(x+.5);y=math.floor(y+.5)
 if x>=0 and y>=0 and x<im.width and y<im.height then im:drawPixel(x,y,colors[color]) end
end
-- Binary coverage with a 1.5px stroke: one/two-pixel clusters, no antialiasing.
local function line(im,x0,y0,x1,y1,color)
 local dx,dy=x1-x0,y1-y0;local length=dx*dx+dy*dy
 for y=math.floor(math.min(y0,y1)-1),math.ceil(math.max(y0,y1)+1) do
  for x=math.floor(math.min(x0,x1)-1),math.ceil(math.max(x0,x1)+1) do
   local t=length>0 and math.max(0,math.min(1,((x-x0)*dx+(y-y0)*dy)/length)) or 0
   if (x-x0-t*dx)^2+(y-y0-t*dy)^2<=.75^2 then put(im,x,y,color) end
  end
 end
end
local function stroke(im,shape,cx,cy,angle,color)
 local co,si=math.cos(angle),math.sin(angle)
 if math.abs(co)<1e-10 then co=0 end;if math.abs(si)<1e-10 then si=0 end
 local function point(p) return cx+S*(p[1]*co-p[2]*si),cy+S*(p[1]*si+p[2]*co) end
 for _,segment in ipairs(shape) do for k=2,#segment do
  local x0,y0=point(segment[k-1]);local x1,y1=point(segment[k]);line(im,x0,y0,x1,y1,color)
 end end
end
local function glyph(im,id,cx,cy,angle) stroke(im,paths[id],math.floor(cx+.5),math.floor(cy+.5),angle or 0,3) end
local function ring(im,cx,cy,r,color)
 local outer=(r+.5)*S;local inner=outer-S
 for y=math.max(0,math.floor(cy-outer)),math.min(im.height-1,math.ceil(cy+outer)) do
  for x=math.max(0,math.floor(cx-outer)),math.min(im.width-1,math.ceil(cx+outer)) do
   local d=(x-cx)^2+(y-cy)^2
   if d<=outer*outer and d>=inner*inner then put(im,x,y,color) end
  end
 end
end
local function dot(im,x,y,core)
 line(im,x-S,y,x+S,y,2);line(im,x,y-S,x,y+S,2);put(im,x,y,core or 1)
end
local function spokes(im,count,r1,r2,offset,color)
 for k=0,count-1 do local a=offset+k*tau/count
  line(im,C+S*r1*math.cos(a),C+S*r1*math.sin(a),C+S*r2*math.cos(a),C+S*r2*math.sin(a),color)
 end
end
local function raw(name,im)
 local f=assert(io.open(out..'/'..name..'.rgba','wb'));f:write(im.bytes);f:close()
end
local function saveImage(name,im)
 local s=Sprite(im.width,im.height,ColorMode.RGB);s.cels[1].image=im
 s:saveCopyAs(out..'/'..name..'.png');s:close();raw(name,im)
end
local function flatten(layers)
 local result=image(N);for _,entry in ipairs(layers) do result:drawImage(entry[2]) end;return result
end
local function saveSource(name,layers)
 local s=Sprite(N,N,ColorMode.RGB)
 for k,entry in ipairs(layers) do
  local layer=k==1 and s.layers[1] or s:newLayer();layer.name=entry[1]
  if k==1 then s.cels[1].image=entry[2] else s:newCel(layer,1,entry[2],Point(0,0)) end
 end
 s:saveAs(out..'/'..name..'.aseprite');s:saveCopyAs(out..'/'..name..'.png');s:close();raw(name,flatten(layers))
end
local medallion=image(15)
ring(medallion,7,7,4.5,2);ring(medallion,7,7,3.5,1);saveImage('medallion',medallion)
local directions=image(16*21,8*21)
for id=1,8 do for k=0,15 do glyph(directions,id,k*21+10,(id-1)*21+10,k*tau/16) end end
saveImage('glyph-directions',directions)
local bases={inner=image(N),outer=image(N)}
local radii={outer={{63.5,2},{60.5,1},{49.5,2},{47.5,1}},inner={{42.5,2},{40.5,1},{outerSigilRadius,2},{largeHalfSide,2},{smallHalfSide,2}}}
for name,entries in pairs(radii) do for _,e in ipairs(entries) do ring(bases[name],C,C,e[1],e[2]) end;saveImage(name..'-base',bases[name]) end
local function compose(name,angle)
 local detail,star,symbols,medals,insetStar=image(N),image(N),image(N),image(N),image(N)
 if name=='outer' then
  spokes(detail,32,61,63,-math.pi/2+angle,2)
  for k=0,15 do local a=-math.pi/2+k*tau/16+angle
   glyph(symbols,k%8+1,C+S*55.5*math.cos(a),C+S*55.5*math.sin(a),a+math.pi/2)
   dot(detail,C+S*61.5*math.cos(a),C+S*61.5*math.sin(a))
  end
 else
  spokes(detail,32,41,42.5,math.pi/32+angle,2)
  local h=largeHalfSide
  local largeSquare={{{-h,-h},{h,-h},{h,h},{-h,h},{-h,-h}}}
  stroke(star,largeSquare,C,C,angle,2)
  stroke(star,largeSquare,C,C,angle+math.pi/4,1)
  -- Small vertices touch the separator; small sides touch the center circle.
  h=smallHalfSide
  local square={{{-h,-h},{h,-h},{h,h},{-h,h},{-h,-h}}}
  stroke(insetStar,square,C,C,angle+math.pi/8,1)
  stroke(insetStar,square,C,C,angle+3*math.pi/8,2)
  for k=0,11 do local a=-math.pi/2+k*tau/12+angle
   local x,y=C+S*35*math.cos(a),C+S*35*math.sin(a)
   if k%3==0 then medals:drawImage(medallion,Point(math.floor(x-7+.5),math.floor(y-7+.5)))
   else glyph(symbols,k%8+1,x,y,a+math.pi/2) end
  end
 end
 return {{'Continuous circular borders',bases[name]},{'Rotating rim marks',detail},{'Overlapping square octagram',star},{'Empty medallions',medals},{'Gold rune strokes',symbols},{'Inner octagram offset 22.5 degrees',insetStar}}
end
local native={}
for _,name in ipairs({'inner','outer'}) do
 local layers=compose(name,0);saveSource(name,layers);native[name]=flatten(layers)
 for _,spec in ipairs({{'rim',2},{'star',3},{'medallions',4},{'runes',5},{'inset-star',6}}) do saveImage(name..'-'..spec[1],layers[spec[2]][2]) end
end
local combined=image(N);combined:drawImage(native.outer);combined:drawImage(native.inner);saveImage('combined-192',combined)
local function scaled(im,factor)
 local s=Sprite(im.width,im.height,ColorMode.RGB);s.cels[1].image=im;s:resize(im.width*factor,im.height*factor)
 local result=s.cels[1].image:clone();s:close();return result
end
saveImage('combined-3x',scaled(combined,3))
local background=pc.rgba(28,23,26,255)
local comparison=image(816,416);comparison:clear(background)
local original=Image{fromFile=baseline..'/combined-192.png'}
comparison:drawImage(scaled(original,2),Point(16,16));comparison:drawImage(scaled(combined,2),Point(416,16))
saveImage('comparison-before-after',comparison)
local frames=120
local contactChecks=0
local function verifyContacts(layers,angle)
 local circular=layers[1][2]
 for _,entry in ipairs({{outerSigilRadius,3,0},{largeHalfSide,3,0},{largeHalfSide,6,math.pi/8},{smallHalfSide,6,math.pi/8}}) do
  local polygon=layers[entry[2]][2]
  for k=0,7 do
   local a=angle+entry[3]+k*tau/8
   local px,py=C+S*entry[1]*math.cos(a),C+S*entry[1]*math.sin(a)
   local touching=false
   for y=math.floor(py-2),math.ceil(py+2) do for x=math.floor(px-2),math.ceil(px+2) do
    if (x-px)^2+(y-py)^2<=4 and pc.rgbaA(circular:getPixel(x,y))>0 and pc.rgbaA(polygon:getPixel(x,y))>0 then touching=true end
   end end
   assert(touching,'Missing circle/square contact at angle '..a)
   contactChecks=contactChecks+1
  end
 end
end
local anim=Sprite(240,240,ColorMode.RGB);anim.layers[1].name='Outer clockwise + inner counterclockwise'
for f=0,frames-1 do
 local angle=f*tau/frames;local frame=image(240);frame:clear(background)
 local innerLayers=compose('inner',-angle);verifyContacts(innerLayers,-angle)
 frame:drawImage(flatten(compose('outer',angle)),Point(24,24));frame:drawImage(flatten(innerLayers),Point(24,24))
 if f>0 then anim:newEmptyFrame() end;anim.frames[f+1].duration=.08
 anim:newCel(anim.layers[1],f+1,frame,Point(0,0))
 if f%15==0 then saveImage('rotation-'..f,frame) end
end
anim:newTag(1,frames).name='OpposingRotation';anim:saveAs(out..'/rotation-native.aseprite')
local palette=Palette(4);palette:setColor(0,Color{r=28,g=23,b=26,a=255})
for i,color in ipairs(colors) do palette:setColor(i,Color{r=pc.rgbaR(color),g=pc.rgbaG(color),b=pc.rgbaB(color),a=255}) end
anim:setPalette(palette);anim:resize(480,480);anim:saveCopyAs(out..'/opposing-rotation.gif');anim:close()
local report=assert(io.open(out..'/contact-verification.json','w'))
report:write(string.format('{"frames":%d,"contactChecks":%d,"circleRadiiPixels":[%.9f,%.9f,%.9f],"relativeOctagramAngleDegrees":22.5,"method":"Shared opaque pixel within2px of each of32 analytic tangencies per frame"}\n',frames,contactChecks,outerSigilRadius*S,largeHalfSide*S,smallHalfSide*S));report:close()
print('Preview only: tangent circle/square pairs; orange geometry, yellow runes; '..contactChecks..' raster contact checks passed.')
