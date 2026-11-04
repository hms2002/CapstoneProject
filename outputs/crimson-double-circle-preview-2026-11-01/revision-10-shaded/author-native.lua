local out=app.params.outputDir
local pc=app.pixelColor
local gold=pc.rgba(255,198,90,255)
local cream=pc.rgba(255,240,191,255)
local ember=pc.rgba(241,107,50,255)
local function put(im,x,y,c)
  x=math.floor(x+.5);y=math.floor(y+.5)
  if x>=0 and y>=0 and x<im.width and y<im.height then im:drawPixel(x,y,c) end
end
local function ringPath(im,cx,cy,r,c)
  for x=.5,r/math.sqrt(2)+.5,1 do
    local y=math.floor(math.sqrt(math.max(0,r*r-x*x)))+.5
    for _,sx in ipairs({-1,1}) do for _,sy in ipairs({-1,1}) do
      put(im,cx+sx*x,cy+sy*y,c);put(im,cx+sx*y,cy+sy*x,c)
    end end
  end
end
local function ring(im,cx,cy,r,c)
  ringPath(im,cx,cy,r,c);ringPath(im,cx,cy,r-1,c)
end
local function medallion(im,cx,cy,c) ring(im,cx,cy,4.5,c) end
local tall={
  {'1111100','1100110','1100110','1111100','1100000','1100000','1100000'},
  {'1100011','1100011','1100011','0111110','0011100','0011100','0011100'},
  {'0011100','0011100','0110110','0110110','1100011','1100011','1100011'},
  {'1111111','1111111','0011100','0011100','0011100','0011100','0011100'},
  {'1111111','1111111','0110000','0011000','0110000','1111111','1111111'},
  {'1100011','1100011','1100011','1101011','1101011','1111111','0110110'}
}
local cardinal={
  {'1111','1001','1001','1001'}, {'1001','1001','1111','0100'},
  {'1001','1001','1011','0110'}, {'1000','1001','0101','0010'}
}
local function glyph(im,pattern,cx,cy,a,c)
  local h,w=#pattern,#pattern[1]
  for y=1,h do for x=1,w do if pattern[y]:sub(x,x)=='1' then
    local dx=x-(w+1)/2;local dy=y-(h+1)/2
    -- Accent the native rune's cap and foot; preserve its original opaque mask.
    local tone=c
    local above=y>1 and pattern[y-1]:sub(x,x)=='1'
    local below=y<h and pattern[y+1]:sub(x,x)=='1'
    if y<=2 and not above then tone=cream
    elseif y>=h-1 and not below then tone=ember end
    put(im,cx+dx*math.cos(a)-dy*math.sin(a),cy+dx*math.sin(a)+dy*math.cos(a),tone)
  end end end
end
for _,spec in ipairs({{'inner',80},{'outer',120}}) do
  local name,n=spec[1],spec[2]
  local c=(n-1)/2
  local layers={{'Gold borders - 2px',Image(n,n,ColorMode.RGB)},{'Octagram',Image(n,n,ColorMode.RGB)},{'Runes - bold',Image(n,n,ColorMode.RGB)},{'Cardinal symbols',Image(n,n,ColorMode.RGB)}}
  local rails,star,runes,accents=layers[1][2],layers[2][2],layers[3][2],layers[4][2]
  if name=='inner' then
    ring(rails,c,c,39.5,gold);ring(rails,c,c,27.5,gold)
    ring(rails,c,c,16.5,gold);ring(rails,c,c,9.5,gold)
    for y=0,n-1 do for x=0,n-1 do
      local dx,dy=math.abs(x-c),math.abs(y-c)
      if (math.max(dx,dy)>=17.5 and math.max(dx,dy)<=18.5) or (dx+dy>=26 and dx+dy<=27) then
        local tone=gold
        -- Contiguous vertex accents, not speckled highlights along every edge.
        if y<c and (dy>=24 or (dy>=17 and dx>=15 and dx<=20)) then tone=cream
        elseif y>c and (dy>=24 or (dy>=17 and dx>=15 and dx<=20)) then tone=ember end
        put(star,x,y,tone)
      end
    end end
    for k=0,11 do
      local a=-math.pi/2+k*math.pi/6
      local gx,gy=c+33*math.cos(a),c+33*math.sin(a)
      if k%3==0 then
        medallion(rails,gx,gy,gold)
        glyph(accents,cardinal[k/3+1],gx,gy,k*math.pi/6,gold)
      else glyph(runes,tall[k%6+1],gx,gy,k*math.pi/6,gold) end
    end
    local centerMarks={{'1100','1100','1111'},{'1111','0011','0011'},{'0011','0011','1111'},{'1111','1100','1100'}}
    for k=0,3 do
      local a=-math.pi/2+k*math.pi/2
      glyph(runes,centerMarks[k+1],c+12.5*math.cos(a),c+12.5*math.sin(a),a+math.pi/2,gold)
    end
  else
    ring(rails,c,c,59.5,gold);ring(rails,c,c,47.5,gold)
    for k=0,15 do
      local a=-math.pi/2+k*math.pi/8
      glyph(runes,tall[k%6+1],c+53*math.cos(a),c+53*math.sin(a),k*math.pi/8,gold)
    end
  end
  -- Borders are the protected single-gold layer, including all medallions.
  for y=0,n-1 do for x=0,n-1 do
    if pc.rgbaA(rails:getPixel(x,y))>0 then
      star:drawPixel(x,y,0);runes:drawPixel(x,y,0);accents:drawPixel(x,y,0)
    end
  end end
  local s=Sprite(n,n,ColorMode.RGB)
  local composite=Image(n,n,ColorMode.RGB)
  for i,specLayer in ipairs(layers) do
    local layer=i==1 and s.layers[1] or s:newLayer()
    layer.name=specLayer[1]
    if i==1 then s.cels[1].image=specLayer[2] else s:newCel(layer,1,specLayer[2],Point(0,0)) end
    composite:drawImage(specLayer[2])
  end
  s:saveAs(out..'/'..name..'.aseprite');s:saveCopyAs(out..'/'..name..'-preview.png')
  local f=assert(io.open(out..'/'..name..'.rgba','wb'));f:write(composite.bytes);f:close()
  local borderFile=assert(io.open(out..'/'..name..'-borders.rgba','wb'));borderFile:write(rails.bytes);borderFile:close()
  s:close()
  print(name..' '..n..'x'..n..' native-grid artwork')
end
