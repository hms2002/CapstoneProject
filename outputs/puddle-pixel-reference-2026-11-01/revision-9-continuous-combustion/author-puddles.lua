-- Aseprite native art: one continuous burning surface, no placed flame shapes.
local out=assert(app.params.outputDir)
local pc=app.pixelColor
local N=64
local function rgba(s) return pc.rgba(tonumber(s:sub(1,2),16),tonumber(s:sub(3,4),16),tonumber(s:sub(5,6),16),255) end
local function colors(list) local c={};for _,s in ipairs(list) do c[#c+1]=rgba(s) end;return c end
local B=colors({'BE7B21','E8AD32','F4C74B','FFDB6B','D99526','E6D39B','FFF0CB','FFFCED'})
local F=colors({'B34522','E65A17','F58119','FFB52B','FFE05A','CE4819','FFF2AF'})
local function alpha(c,a) return pc.rgba(pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c),a) end
-- Reference-informed thin liquid film; foam and hot spots remain crisp.
for i,a in ipairs({144,154,148,178,138,180,218,240}) do B[i]=alpha(B[i],a) end
for i,a in ipairs({174,220,242,252,255,208,255}) do F[i]=alpha(F[i],a) end
local function im(w,h) return Image(w or N,h or N,ColorMode.RGB) end
local function put(dst,x,y,c)
  x=math.floor(x+.5);y=math.floor(y+.5)
  if x>=0 and y>=0 and x<dst.width and y<dst.height then dst:drawPixel(x,y,c) end
end
local function poly(dst,pts,c)
  for y=0,dst.height-1 do
    local xs={}
    for i,a in ipairs(pts) do local b=pts[i%#pts+1]
      if (a[2]<=y and b[2]>y) or (b[2]<=y and a[2]>y) then xs[#xs+1]=a[1]+(y-a[2])*(b[1]-a[1])/(b[2]-a[2]) end
    end
    table.sort(xs)
    for k=1,#xs-1,2 do for x=math.ceil(xs[k]),math.floor(xs[k+1]) do put(dst,x,y,c) end end
  end
end
local function line(dst,x1,y1,x2,y2,c)
  local dx,dy=x2-x1,y2-y1;local steps=math.max(math.abs(dx),math.abs(dy))
  for i=0,steps do local t=steps==0 and 0 or i/steps;put(dst,x1+t*dx,y1+t*dy,c) end
end
local function ellipse(dst,cx,cy,rx,ry,c)
  for y=math.floor(cy-ry),math.ceil(cy+ry) do for x=math.floor(cx-rx),math.ceil(cx+rx) do
    if ((x-cx)/rx)^2+((y-cy)/ry)^2<=1 then put(dst,x,y,c) end
  end end
end
local function arc(dst,cx,cy,rx,ry,a,b,c)
  for d=a,b,3 do local t=d*math.pi/180;put(dst,cx+rx*math.cos(t),cy+ry*math.sin(t),c) end
end
local outline={{29,8},{35,8},{39,11},{44,11},{48,15},{48,20},{53,23},{55,28},{52,32},{54,37},{52,42},{47,44},{46,49},{41,52},{36,51},{32,55},{26,54},{22,51},{17,51},{12,47},{13,42},{8,39},{7,33},{10,28},{9,23},{12,18},{18,17},{20,12},{25,12}}
local mask=im();poly(mask,outline,rgba('FFFFFF'))
local function inside(x,y) return x>=0 and y>=0 and x<N and y<N and mask:getPixel(x,y)~=0 end
local function clip(dst)
  for y=0,N-1 do for x=0,N-1 do if not inside(x,y) then dst:drawPixel(x,y,0) end end end
end
local function edge(x,y) return inside(x,y) and (not inside(x-1,y) or not inside(x+1,y) or not inside(x,y-1) or not inside(x,y+1)) end
local function ground(border,base)
  local dst=im()
  for y=0,N-1 do for x=0,N-1 do if inside(x,y) then put(dst,x,y,edge(x,y) and border or base) end end end
  return dst
end
local function flatten(images,w,h)
  local dst=im(w,h);for _,a in ipairs(images) do dst:drawImage(a) end;return dst
end
local function separateCoverage(images)
  -- Let each visible pixel belong to one editable layer so the limited RGBA palette
  -- stays exact; no repeated alpha blending creates unintended intermediate colors.
  for y=0,N-1 do for x=0,N-1 do
    local covered=false
    for i=#images,1,-1 do
      if images[i]:getPixel(x,y)~=0 then
        if covered then images[i]:drawPixel(x,y,0) else covered=true end
      end
    end
  end end
  return images
end
local function exactReviewPalette(s)
  local seen,values={},{}
  for f=1,#s.frames do
    local a=im(s.width,s.height);a:drawSprite(s,f)
    for px in a:pixels() do local c=px()
      if pc.rgbaA(c)>0 and not seen[c] then seen[c]=true;values[#values+1]=c end
    end
  end
  assert(#values<256,'Review exceeds exact GIF palette')
  local palette=Palette(#values+1);palette:setColor(0,Color{r=0,g=0,b=0,a=0})
  for i,c in ipairs(values) do palette:setColor(i,c) end;s:setPalette(palette)
end
local function foamPatch(dst,x,y,rows,f)
  for py,row in ipairs(rows) do for px=1,#row do
    local n=tonumber(row:sub(px,px))
    if n then put(dst,x+px-1,y+py-1,B[n]) end
  end end
  if f==1 then put(dst,x+3,y+1,B[8]) elseif f==3 then put(dst,x+4,y+2,B[6]) end
end
local function beer(f)
  local base=ground(B[1],B[2])
  poly(base,{{14,21},{24,17},{35,18},{39,22},{34,26},{25,25},{20,28},{13,27}},B[3])
  poly(base,{{29,35},{38,29},{47,31},{49,36},{44,41},{37,43},{30,40}},B[3])
  poly(base,{{17,39},{25,38},{28,42},{34,45},{32,49},{24,48},{20,44},{15,44}},B[3])
  -- Honey-gold swirls remain low contrast; no brown/black specular pools.
  poly(base,{{24,29},{31,27},{35,29},{33,31},{28,32},{24,34},{20,32}},B[5])
  local surface=im();local sh=({0,1,0,-1})[f+1]
  arc(surface,29,32,13,11,200+sh*3,247+sh*3,B[4])
  arc(surface,32,33,13,12,15-sh*4,53-sh*4,B[4])
  line(surface,28+sh,21,32+sh,21,B[4])
  line(surface,19,41-sh,22,41-sh,B[4])
  local foam=im()
  -- Uneven accumulation inside the spill, leaving most of the rim bare.
  foamPatch(foam,17,21,{'...77.....','..78877...','.7777767..','..66777...','.....66...'},f)
  foamPatch(foam,24,26,{'..77.','.7877','..766'},(f+1)%4)
  foamPatch(foam,37,37,{'..77...','.7887..','..7766.','....6..'},(f+2)%4)
  for _,p in ipairs({{15,26},{16,27},{27,22},{30,31},{42,35},{34,44}}) do put(foam,p[1],p[2],B[7]) end
  -- Two tiny bubbles swell and pop, never forming a large connected foam cap.
  for i,p in ipairs({{24,30},{39,37}}) do
    local stage=(f+i)%4
    if stage==0 then put(foam,p[1],p[2],B[7])
    elseif stage==1 then put(foam,p[1],p[2]-1,B[8]);put(foam,p[1]-1,p[2],B[7]);put(foam,p[1]+1,p[2],B[6])
    elseif stage==2 then put(foam,p[1]-2,p[2],B[7]);put(foam,p[1]+2,p[2],B[6]) end
  end
  for _,a in ipairs({base,surface,foam}) do clip(a) end
  return separateCoverage({base,surface,foam,im()})
end
-- A single continuous field is evaluated across the full footprint. There are
-- no local centers, roots, flame silhouettes, stamps, or particle emitters.
local function noiseCorner(x,y,k)
  local v=math.sin(x*127.1+y*311.7+k*74.7)*43758.5453
  return v-math.floor(v)
end
local function noise(x,y,k)
  local ix,iy=math.floor(x),math.floor(y)
  local u,v=x-ix,y-iy
  u=u*u*(3-2*u);v=v*v*(3-2*v)
  local a=noiseCorner(ix,iy,k)*(1-u)+noiseCorner(ix+1,iy,k)*u
  local b=noiseCorner(ix,iy+1,k)*(1-u)+noiseCorner(ix+1,iy+1,k)*u
  return a*(1-v)+b*v
end
local function combustion(x,y,k)
  return .52*noise(x/10,y/9,k+19)+.33*noise(x/5,y/4.5,k+71)+.15*noise(x/2.4,y/2.7,k+131)
end
local function burn(f)
  f=(f+3)%12
  local base,heat,glow=im(),im(),im()
  local k=math.floor(f/2);local blend=(f%2)/2
  for y=0,N-1 do for x=0,N-1 do if inside(x,y) then
    local v=combustion(x,y,k)*(1-blend)+combustion(x,y,(k+1)%6)*blend
    local n
    if edge(x,y) then n=1
    elseif v<.30 then n=6
    elseif v<.39 then n=2
    elseif v<.46 then n=3
    elseif v<.52 then n=4
    elseif v<.65 then n=5
    else n=7 end
    put((n<=2 or n==6) and base or n<=4 and heat or glow,x,y,F[n])
  end end end
  return {base,heat,glow,im()}
end
if app.params.draftOnly=='true' then
  local s=Sprite(80,80,ColorMode.RGB);local contact=im(64*6,64*2)
  for f=0,11 do
    if f>0 then s:newEmptyFrame() end;s.frames[f+1].duration=.08
    local a=im(80,80)
    for y=0,79 do for x=0,79 do put(a,x,y,(x%16==0 or y%16==0) and rgba('2B343A') or rgba('343E45')) end end
    local art=flatten(burn(f));a:drawImage(art,Point(8,8))
    local old=s.layers[1]:cel(f+1);if old then s:deleteCel(old) end
    s:newCel(s.layers[1],f+1,a,Point(0,0))
    contact:drawImage(art,Point((f%6)*64,math.floor(f/6)*64))
  end
  s:resize(480,480);exactReviewPalette(s);s:saveAs(out..'/surface-draft.aseprite');s:saveCopyAs(out..'/surface-draft.gif');Image(s):saveAs(out..'/surface-draft.png')
  contact:resize(1152,384);contact:saveAs(out..'/surface-draft-frames.png');s:close();return
end
local beerFrames,fireFrames,igniteFrames={},{},{}
for f=0,3 do beerFrames[#beerFrames+1]={images=beer(f),ms=160} end
for f=0,11 do fireFrames[#fireFrames+1]={images=burn(f),ms=80} end
local radii={-20,5,11,18,26,34,41,100}
local counts={}
for f=0,7 do
  if f==0 then igniteFrames[1]={images=beerFrames[1].images,ms=80};counts[1]=0
  elseif f==7 then igniteFrames[8]={images=fireFrames[1].images,ms=80};counts[8]=-1
  else
    local before=beerFrames[f%4+1].images;local after=fireFrames[(f-1)%12+1].images
    local layers={im(),im(),im(),im()};local burned=0
    for y=0,N-1 do for x=0,N-1 do if inside(x,y) then
      local dx,dy=x-12,y-32
      local ang=math.atan(dy,dx)
      local distance=math.sqrt(dx*dx+dy*dy)+math.sin(ang*5)*1.8+math.sin(ang*11)*.8
      local d=radii[f+1]-distance
      if d>=0 then burned=burned+1 end
      for k=1,3 do put(layers[k],x,y,(d>=0 and after[k] or before[k]):getPixel(x,y)) end
      if d>=0 and d<1.3 then put(layers[4],x,y,F[7])
      elseif d>=1.3 and d<2.8 then put(layers[4],x,y,F[5])
      elseif d>=2.8 and d<4.2 then put(layers[4],x,y,F[4]) end
    end end end
    igniteFrames[f+1]={images=separateCoverage(layers),ms=80};counts[f+1]=burned
  end
end
local allPalette={};for _,a in ipairs({B,F}) do for _,c in ipairs(a) do
  local found=false;for _,v in ipairs(allPalette) do if v==c then found=true end end
  if not found then allPalette[#allPalette+1]=c end
end end
local function make(name,w,h,frames,pal,names,tags)
  local s=Sprite(w,h,ColorMode.RGB);s.gridBounds=Rectangle(0,0,1,1)
  local p=Palette(#pal+1);p:setColor(0,Color{r=0,g=0,b=0,a=0})
  for k,c in ipairs(pal) do p:setColor(k,c) end;s:setPalette(p)
  local layers={s.layers[1]};for k=2,#names do layers[k]=s:newLayer() end
  for k,l in ipairs(layers) do l.name=names[k] end
  for f,frame in ipairs(frames) do
    if f>1 then s:newEmptyFrame() end;s.frames[f].duration=frame.ms/1000
    for k,a in ipairs(frame.images) do
      local old=layers[k]:cel(f);if old then s:deleteCel(old) end
      s:newCel(layers[k],f,a,Point(0,0))
    end
    flatten(frame.images,w,h):saveAs(out..'/'..name..'-frame-'..f..'.png')
  end
  for _,a in ipairs(tags) do local tag=s:newTag(a[2],a[3]);tag.name=a[1] end
  local slice=s:newSlice(Rectangle(0,0,w,h));slice.name=w==64 and 'GroundCenter' or 'ParticleBase';slice.pivot=w==64 and Point(32,32) or Point(w/2,h-2)
  s.data='Aseprite native pixels. Continuous whole-surface combustion, preserved beer. No flying particle overlays. Preview only.'
  s:saveAs(out..'/'..name..'.aseprite')
  -- GIF cannot preserve partial alpha: its review copy is explicitly composited.
  if w~=64 then s:saveCopyAs(out..'/'..name..'.gif') else
    local review=Sprite(w,h,ColorMode.RGB)
    for f,frame in ipairs(frames) do
      if f>1 then review:newEmptyFrame() end;review.frames[f].duration=frame.ms/1000
      local a=im(w,h)
      for y=0,h-1 do for x=0,w-1 do put(a,x,y,(x%16==0 or y%16==0) and rgba('2B343A') or rgba('343E45')) end end
      a:drawImage(flatten(frame.images,w,h))
      local old=review.layers[1]:cel(f);if old then review:deleteCel(old) end
      review:newCel(review.layers[1],f,a,Point(0,0))
    end
    exactReviewPalette(review);review:saveAs(out..'/'..name..'.gif');review:close()
  end
  local cols=math.min(#frames,8);local sheet=im(w*cols,h*math.ceil(#frames/cols))
  for f,frame in ipairs(frames) do sheet:drawImage(flatten(frame.images,w,h),Point((f-1)%cols*w,math.floor((f-1)/cols)*h)) end
  sheet:saveAs(out..'/'..name..'-sheet.png')
  return s
end
local names={'01 Liquid / burning ground','02 Surface motion','03 Foam / surface cores','04 Ignition front'}
local sprites={}
sprites.beer=make('beer',64,64,beerFrames,B,names,{{'BeerIdle',1,4}})
sprites.fire=make('fire-surface',64,64,fireFrames,F,names,{{'FireLoop',1,12}})
sprites.ignite=make('beer-to-fire',64,64,igniteFrames,allPalette,names,{{'Ignite',1,8}})
local master={};for _,frames in ipairs({beerFrames,igniteFrames}) do for _,fr in ipairs(frames) do master[#master+1]=fr end end
-- Ignite finishes on Fire frame1; start the master loop on frame2 to avoid a duplicate hold.
for i=1,12 do master[#master+1]=fireFrames[i%12+1] end
sprites.master=make('puddle-cycle',64,64,master,allPalette,names,{{'BeerIdle',1,4},{'Ignite',5,12},{'FireLoop',13,24}})
local function neutral(w,h)
  local dst=im(w,h)
  for y=0,h-1 do for x=0,w-1 do put(dst,x,y,((x%16==0 or y%16==0) and rgba('2B343A') or rgba('343E45'))) end end
  return dst
end
local function preview(name,w,h,draw,count,ms,scale)
  local s=Sprite(w,h,ColorMode.RGB);s.layers[1].name='Preview - neutral ground, not Unity capture'
  for f=0,count-1 do
    if f>0 then s:newEmptyFrame() end;s.frames[f+1].duration=ms/1000
    local a=draw(f*ms);local old=s.layers[1]:cel(f+1);if old then s:deleteCel(old) end
    s:newCel(s.layers[1],f+1,a,Point(0,0))
  end
  s:resize(w*scale,h*scale);exactReviewPalette(s);s:saveAs(out..'/'..name..'.aseprite');s:saveCopyAs(out..'/'..name..'.gif')
  Image(s):saveAs(out..'/'..name..'.png');s:close()
end
local function stageAt(t)
  if t<640 then return beerFrames[math.floor(t/160)%4+1]
  elseif t<1280 then return igniteFrames[math.floor((t-640)/80)+1]
  elseif t<3520 then return fireFrames[(math.floor((t-1280)/80)+1)%12+1]
  else return nil end -- A visible blank reset separates repetitions of the demonstration.
end
preview('comparison-preview',240,80,function(t)
  local dst=neutral(240,80)
  line(dst,79,0,79,79,rgba('52616B'));line(dst,159,0,159,79,rgba('52616B'))
  dst:drawImage(flatten(beerFrames[math.floor(t/160)%4+1].images),Point(8,8))
  local current=stageAt(t);if current then dst:drawImage(flatten(current.images),Point(88,8)) end
  dst:drawImage(flatten(fireFrames[math.floor(t/80)%12+1].images),Point(168,8))
  return dst
end,96,40,4)
preview('ignition-preview',80,80,function(t)
  local dst=neutral(80,80);local current=stageAt(t)
  if current then dst:drawImage(flatten(current.images),Point(8,8)) end
  return dst
end,96,40,4)
preview('surface-only-preview',80,80,function(t)
  local dst=neutral(80,80);dst:drawImage(flatten(fireFrames[math.floor(t/80)%12+1].images),Point(8,8));return dst
end,12,80,6)
local contact=neutral(8*64,64*4)
for i,fr in ipairs(beerFrames) do contact:drawImage(flatten(fr.images),Point((i-1)*64,0)) end
for i,fr in ipairs(igniteFrames) do contact:drawImage(flatten(fr.images),Point((i-1)*64,64)) end
for i,fr in ipairs(fireFrames) do contact:drawImage(flatten(fr.images),Point((i-1)%8*64,128+math.floor((i-1)/8)*64)) end
contact:resize(1024,512);contact:saveAs(out..'/all-frames-2x.png')
for _,s in pairs(sprites) do s:close() end
local log=assert(io.open(out..'/authoring-complete.txt','w'))
log:write('Aseprite '..tostring(app.version)..'\nBeer4 / Ignite8 / Fire12\nNative64x64 ground, pivot32,32.\nFire loop960ms; continuous whole-surface combustion, no flame stamps or emitters.\nIgnition burned pixel counts: '..table.concat(counts,',')..'\n')
log:close()
