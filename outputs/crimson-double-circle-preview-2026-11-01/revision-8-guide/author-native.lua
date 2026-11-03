local out=app.params.outputDir
local pc=app.pixelColor
local gold=pc.rgba(255,198,90,255)
local orange=pc.rgba(241,107,50,255)
local ivory=pc.rgba(255,240,191,255)
local function put(im,x,y,c)
  x=math.floor(x+.5);y=math.floor(y+.5)
  if x>=0 and y>=0 and x<im.width and y<im.height then im:drawPixel(x,y,c) end
end
local function ring(im,cx,cy,r,c)
  for x=.5,r/math.sqrt(2)+.5,1 do
    local y=math.floor(math.sqrt(math.max(0,r*r-x*x)))+.5
    for _,sx in ipairs({-1,1}) do for _,sy in ipairs({-1,1}) do
      put(im,cx+sx*x,cy+sy*y,c);put(im,cx+sx*y,cy+sy*x,c)
    end end
  end
end
local function medallion(im,cx,cy,c) ring(im,cx,cy,3.5,c) end
local small={
  {'110','010','011'}, {'101','111','010'}, {'100','111','001'}, {'010','110','011'},
  {'111','100','110'}, {'011','010','110'}, {'110','101','100'}, {'100','110','011'}
}
local tall={
  {'11110','10001','11110','10000','10000'}, {'10001','10001','01110','00100','00100'},
  {'00100','01010','01010','10001','10001'}, {'11111','00100','00100','00100','00100'},
  {'11111','01000','00100','01000','11111'}, {'10001','10001','10101','10101','01110'}
}
local cardinal={
  {'1111','1001','1001','1001'}, {'1001','1001','1111','0100'},
  {'1001','1001','1011','0110'}, {'1000','1001','0101','0010'}
}
local function glyph(im,pattern,cx,cy,a,c)
  local h,w=#pattern,#pattern[1]
  for y=1,h do for x=1,w do if pattern[y]:sub(x,x)=='1' then
    local dx=x-(w+1)/2;local dy=y-(h+1)/2
    put(im,cx+dx*math.cos(a)-dy*math.sin(a),cy+dx*math.sin(a)+dy*math.cos(a),c)
  end end end
end
for _,spec in ipairs({{'inner',80},{'outer',120}}) do
  local name,n=spec[1],spec[2]
  local c=(n-1)/2
  local layers={{'Circular rails',Image(n,n,ColorMode.RGB)},{'Octagram',Image(n,n,ColorMode.RGB)},{'Runes',Image(n,n,ColorMode.RGB)},{'Accents',Image(n,n,ColorMode.RGB)}}
  local rails,star,runes,accents=layers[1][2],layers[2][2],layers[3][2],layers[4][2]
  if name=='inner' then
    ring(rails,c,c,39.5,gold);ring(rails,c,c,30.5,orange)
    ring(rails,c,c,18.5,orange);ring(rails,c,c,13.5,gold)
    for y=0,n-1 do for x=0,n-1 do
      local dx,dy=math.abs(x-c),math.abs(y-c)
      if (math.max(dx,dy)==20.5 and math.min(dx,dy)<=20.5) or dx+dy==30 then
        put(star,x,y,gold)
      end
    end end
    for k=0,11 do
      local a=-math.pi/2+k*math.pi/6
      local gx,gy=c+35*math.cos(a),c+35*math.sin(a)
      if k%3==0 then
        medallion(runes,gx,gy,gold)
        glyph(accents,cardinal[k/3+1],gx,gy,k*math.pi/6,ivory)
      else glyph(runes,tall[k%6+1],gx,gy,k*math.pi/6,gold) end
    end
    local centerMarks={{'110','010','011'},{'100','111','001'},{'011','010','110'},{'001','111','100'}}
    for k=0,3 do
      local a=-math.pi/2+k*math.pi/2
      glyph(runes,centerMarks[k+1],c+16*math.cos(a),c+16*math.sin(a),a+math.pi/2,gold)
    end
    for _,p in ipairs({{c-20.5,c-20.5},{c+20.5,c-20.5},{c-20.5,c+20.5},{c+20.5,c+20.5},{c-.5,c-29.5},{c+.5,c-29.5},{c-.5,c+29.5},{c+.5,c+29.5},{c-29.5,c-.5},{c-29.5,c+.5},{c+29.5,c-.5},{c+29.5,c+.5}}) do put(accents,p[1],p[2],ivory) end
  else
    ring(rails,c,c,59.5,gold);ring(rails,c,c,50.5,orange)
    for k=0,11 do
      local a=-math.pi/2+k*math.pi/6
      glyph(runes,tall[k%6+1],c+55*math.cos(a),c+55*math.sin(a),k*math.pi/6,k%3==0 and ivory or gold)
    end
  end
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
  s:close()
  print(name..' '..n..'x'..n..' native-grid artwork')
end
