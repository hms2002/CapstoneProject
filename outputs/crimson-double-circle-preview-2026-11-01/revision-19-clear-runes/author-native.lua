local out=assert(app.params.outputDir)
local pc=app.pixelColor
local colors={pc.rgba(182,61,40,255),pc.rgba(241,107,50,255),pc.rgba(255,198,90,255),pc.rgba(255,240,191,255)}
local N,C=128,63.5
local tau=math.pi*2
-- Explicit connected strokes, authored at 5x9. Rotation rasterizes the strokes
-- once in their final orientation, rather than rotating an already tiny bitmap.
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
local function line(im,x0,y0,x1,y1,color)
  x0=math.floor(x0+.5);y0=math.floor(y0+.5);x1=math.floor(x1+.5);y1=math.floor(y1+.5)
  local dx,sx=math.abs(x1-x0),x0<x1 and 1 or -1
  local dy,sy=-math.abs(y1-y0),y0<y1 and 1 or -1
  local err=dx+dy
  while true do
    put(im,x0,y0,color);if x0==x1 and y0==y1 then break end
    local twice=2*err
    if twice>=dy then err=err+dy;x0=x0+sx end
    if twice<=dx then err=err+dx;y0=y0+sy end
  end
end
local function stroke(im,shape,cx,cy,angle,color,scale)
  local co,si=math.cos(angle),math.sin(angle);scale=scale or 1
  if math.abs(co)<1e-10 then co=0 end;if math.abs(si)<1e-10 then si=0 end
  local function point(p) return cx+scale*(p[1]*co-p[2]*si),cy+scale*(p[1]*si+p[2]*co) end
  for _,segment in ipairs(shape) do for k=2,#segment do
    local x0,y0=point(segment[k-1]);local x1,y1=point(segment[k])
    line(im,x0,y0,x1,y1,color)
  end end
end
local function glyph(im,id,cx,cy,angle)
  stroke(im,paths[id],math.floor(cx+.5),math.floor(cy+.5),angle or 0,3)
end
local function ring(im,cx,cy,r,color)
  local outer=r+.5
  local function inside(x,y) return (x-cx)^2+(y-cy)^2<=outer*outer end
  for y=math.max(0,math.floor(cy-outer)),math.min(im.height-1,math.ceil(cy+outer)) do
    for x=math.max(0,math.floor(cx-outer)),math.min(im.width-1,math.ceil(cx+outer)) do
      if inside(x,y) and (not inside(x-1,y) or not inside(x+1,y) or not inside(x,y-1) or not inside(x,y+1)) then put(im,x,y,color) end
    end
  end
end
local function dot(im,x,y,core)
  put(im,x-1,y,2);put(im,x+1,y,2);put(im,x,y-1,2);put(im,x,y+1,2);put(im,x,y,core or 3)
end
local function spokes(im,count,r1,r2,offset,color)
  for k=0,count-1 do local a=offset+k*tau/count
    line(im,C+r1*math.cos(a),C+r1*math.sin(a),C+r2*math.cos(a),C+r2*math.sin(a),color)
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
  s:saveAs(out..'/'..name..'.aseprite');s:saveCopyAs(out..'/'..name..'.png');s:close()
  raw(name,flatten(layers))
end
local medallion=image(10)
ring(medallion,4.5,4.5,4.5,2);ring(medallion,4.5,4.5,3.5,1)
saveImage('medallion',medallion)
local atlas=image(132,11)
for id=1,#paths do glyph(atlas,id,(id-1)*11+5,5) end
saveImage('glyph-atlas',atlas)
local atlasSource=Sprite(132,11,ColorMode.RGB);atlasSource.layers[1].name='Connected single-color rune strokes';atlasSource.cels[1].image=atlas
atlasSource:saveAs(out..'/glyph-atlas.aseprite');atlasSource:close()
-- Direction atlas is an editable diagnostic, not a required runtime animation.
local directions=image(16*13,8*13)
for id=1,8 do for k=0,15 do glyph(directions,id,k*13+6,(id-1)*13+6,k*tau/16) end end
saveImage('glyph-directions',directions)
local bases={inner=image(N),outer=image(N)}
local radii={outer={{63.5,2},{60.5,1},{49.5,2},{47.5,1}},inner={{42.5,2},{40.5,1},{29.5,2},{18.5,1},{16.5,2},{11.5,3}}}
for name,entries in pairs(radii) do for _,e in ipairs(entries) do ring(bases[name],C,C,e[1],e[2]) end;saveImage(name..'-base',bases[name]) end
local function compose(name,angle)
  local detail,star,symbols,medals=image(N),image(N),image(N),image(N)
  if name=='outer' then
    spokes(detail,32,61,63,-math.pi/2+angle,2)
    for k=0,15 do local a=-math.pi/2+k*tau/16+angle
      glyph(symbols,k%8+1,C+55.5*math.cos(a),C+55.5*math.sin(a),a+math.pi/2)
      dot(detail,C+61.5*math.cos(a),C+61.5*math.sin(a))
    end
  else
    spokes(detail,32,41,42.5,math.pi/32+angle,2)
    stroke(star,{{{-19.5,-19.5},{19.5,-19.5},{19.5,19.5},{-19.5,19.5},{-19.5,-19.5}}},C,C,angle,2)
    stroke(star,{{{0,-27.5},{27.5,0},{0,27.5},{-27.5,0},{0,-27.5}}},C,C,angle,3)
    for k=0,7 do local a=k*tau/8+angle;dot(star,C+27.5*math.cos(a),C+27.5*math.sin(a),k%2==0 and 4 or 3) end
    for k=0,3 do local a=-math.pi/2+k*tau/4+angle
      glyph(symbols,9+k,C+14.5*math.cos(a),C+14.5*math.sin(a),a+math.pi/2)
    end
    for k=0,11 do local a=-math.pi/2+k*tau/12+angle
      local x,y=C+35*math.cos(a),C+35*math.sin(a)
      if k%3==0 then
        medals:drawImage(medallion,Point(math.floor(x-4.5+.5),math.floor(y-4.5+.5)))
      else glyph(symbols,k%8+1,x,y,a+math.pi/2) end
    end
  end
  return {{'Continuous circular borders',bases[name]},{'Rotating rim marks',detail},{'Overlapping square octagram',star},{'Original-size empty medallions',medals},{'Connected gold rune strokes',symbols}}
end
local native={}
for _,name in ipairs({'inner','outer'}) do
  local layers=compose(name,0);saveSource(name,layers);native[name]=flatten(layers)
  for _,spec in ipairs({{'rim',2},{'star',3},{'medallions',4},{'runes',5}}) do saveImage(name..'-'..spec[1],layers[spec[2]][2]) end
end
local combined=image(N);combined:drawImage(native.outer);combined:drawImage(native.inner);saveImage('combined-128',combined)
local function enlarged(name,im,factor)
  local s=Sprite(im.width,im.height,ColorMode.RGB);s.cels[1].image=im;s:resize(im.width*factor,im.height*factor)
  s:saveCopyAs(out..'/'..name..'.png');s:close()
end
enlarged('combined-4x',combined,4);enlarged('glyph-directions-4x',directions,4)
local background=pc.rgba(28,23,26,255)
local frames=120
local anim=Sprite(160,160,ColorMode.RGB);anim.layers[1].name='Outer clockwise + inner counterclockwise'
local manifest=assert(io.open(out..'/rotation-frames.csv','w'));manifest:write('frame,duration_ms,inner_degrees,outer_degrees\n')
for f=0,frames-1 do
  local angle=f*tau/frames
  local outer=flatten(compose('outer',angle));local inner=flatten(compose('inner',-angle))
  local frame=image(160);frame:clear(background)
  frame:drawImage(outer,Point(16,16));frame:drawImage(inner,Point(16,16))
  if f>0 then anim:newEmptyFrame() end;anim.frames[f+1].duration=.08
  anim:newCel(anim.layers[1],f+1,frame,Point(0,0))
  if f%15==0 then saveImage('rotation-'..f,frame) end
  manifest:write(string.format('%d,80,%.6f,%.6f\n',f+1,-f*360/frames,f*360/frames))
end
manifest:close()
anim:newTag(1,frames).name='OpposingRotation'
anim:saveAs(out..'/rotation-native.aseprite')
-- Exact five-color palette prevents GIF color drift; no translucent source colors.
local palette=Palette(5);palette:setColor(0,Color{r=28,g=23,b=26,a=255})
for i,color in ipairs(colors) do palette:setColor(i,Color{r=pc.rgbaR(color),g=pc.rgbaG(color),b=pc.rgbaB(color),a=255}) end
anim:setPalette(palette);anim:resize(480,480)
anim:saveCopyAs(out..'/opposing-rotation.gif');anim:close()
print('Clear 5x9 connected rune strokes; original empty 10px medallions; 120-frame opposite-rotation GIF exported')
