-- Selected Lumen Orb: fixed pixels, smooth color-only cycle, authored in native Aseprite.
local output=assert(app.params.output,'output directory required')
local rgba=app.pixelColor.rgba
local function channels(hex)
  return {tonumber(hex:sub(1,2),16),tonumber(hex:sub(3,4),16),tonumber(hex:sub(5,6),16)}
end
local function color(hex) local c=channels(hex);return rgba(c[1],c[2],c[3],255) end
local keys={
  {name='A Leaf',colors={'315C36','65AD4C','A4DD67','E9FFB8'}},
  {name='B Lime',colors={'3E622F','80C147','C2EE55','F3FFAD'}},
  {name='C Lemon',colors={'4F612E','A0BD46','DDE866','FFF9BB'}},
  {name='E Pear',colors={'5D5E30','ADB349','E6DA78','FFF2C4'}},
  {name='D Mint',colors={'305D42','68B96B','ACE49A','E9FFD0'}}
}
local rows={'..oooo..','.ommmmo.','omllllmo','omlcclmo','omlcclmo','omllllmo','.ommmmo.','..oooo..'}
local roles={o=1,m=2,l=3,c=4}
local function rect(im,x,y,w,h,c)
  for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,c) end end
end
local function stamp(dst,src,x,y,scale)
  for yy=0,src.height-1 do for xx=0,src.width-1 do
    local c=src:getPixel(xx,yy)
    if app.pixelColor.rgbaA(c)>0 then rect(dst,x+xx*scale,y+yy*scale,scale,scale,c) end
  end end
end
local glyphs={
 A={'010','101','111','101','101'},B={'110','101','110','101','110'},C={'011','100','100','100','011'},
 D={'110','101','101','101','110'},E={'111','100','110','100','111'},F={'111','100','110','100','100'},
 G={'011','100','101','101','011'},H={'101','101','111','101','101'},I={'111','010','010','010','111'},
 K={'101','101','110','101','101'},L={'100','100','100','100','111'},M={'101','111','111','101','101'},
 N={'101','111','111','111','101'},O={'010','101','101','101','010'},P={'110','101','110','100','100'},
 R={'110','101','110','101','101'},S={'011','100','010','001','110'},T={'111','010','010','010','010'},
 U={'101','101','101','101','111'},V={'101','101','101','101','010'},W={'101','101','111','111','101'},
 X={'101','101','010','101','101'},Y={'101','101','010','010','010'},
 ['0']={'111','101','101','101','111'},['1']={'010','110','010','010','111'},['2']={'110','001','010','100','111'},
 ['4']={'101','101','111','001','001'},['6']={'011','100','110','101','010'},['8']={'010','101','010','101','010'},
 ['/']={'001','001','010','100','100'},['.']={'000','000','000','000','010'}
}
local function text(im,s,x,y,c,scale)
  scale=scale or 1
  for i=1,#s do
    local g=glyphs[s:sub(i,i)]
    if g then for yy=1,5 do for xx=1,3 do
      if g[yy]:sub(xx,xx)=='1' then rect(im,x+(xx-1)*scale,y+(yy-1)*scale,scale,scale,c) end
    end end end
    x=x+4*scale
  end
end
local sprite=Sprite(16,16,ColorMode.RGB)
sprite.layers[1].name='Fixed silhouette / color cycle'
local core=sprite:newLayer();core.name='Fixed center / color cycle'
local board=Sprite(256,192,ColorMode.RGB)
board.layers[1].name='Preview backdrop only'
local orbLayer=board:newLayer();orbLayer.name='Stationary color-cycle preview'
local backdrop=Image(256,192)
rect(backdrop,0,0,256,192,color('171C29'))
text(backdrop,'LUMEN ORB / COLOR LOOP',12,12,color('EBEBD6'))
text(backdrop,'FIXED POSITION / 4 SECOND LOOP',12,24,color('9BAABB'))
rect(backdrop,12,38,232,105,color('222B3A'))
text(backdrop,'DARK',14,148,color('9BAABB'));text(backdrop,'STONE',137,148,color('9BAABB'))
rect(backdrop,12,158,111,25,color('111923'))
rect(backdrop,133,158,111,25,color('7F7563'))
local sheet=Image(640,16)
for f=0,39 do
  local section=math.floor(f/8)+1
  local nextSection=section%#keys+1
  local t=(f%8)/8
  t=t*t*(3-2*t)
  local p={}
  for r=1,4 do
    local a,b=channels(keys[section].colors[r]),channels(keys[nextSection].colors[r])
    p[r]=rgba(math.floor(a[1]+(b[1]-a[1])*t+.5),math.floor(a[2]+(b[2]-a[2])*t+.5),math.floor(a[3]+(b[3]-a[3])*t+.5),255)
  end
  local body,center,merged=Image(16,16),Image(16,16),Image(16,16)
  for y,row in ipairs(rows) do for x=1,#row do
    local k=row:sub(x,x)
    if roles[k] then
      local c=p[roles[k]]
      local target=k=='c' and center or body
      target:drawPixel(x+3,y+3,c);merged:drawPixel(x+3,y+3,c)
    end
  end end
  if f>0 then sprite:newEmptyFrame();board:newEmptyFrame() end
  sprite.frames[f+1].duration=.1;board.frames[f+1].duration=.1
  sprite:newCel(sprite.layers[1],f+1,body,Point(0,0))
  sprite:newCel(core,f+1,center,Point(0,0))
  stamp(sheet,merged,f*16,0,1)
  board:newCel(board.layers[1],f+1,backdrop,Point(0,0))
  local preview=Image(256,192)
  stamp(preview,merged,80,42,6)
  for _,x in ipairs({26,60,94,147,181,215}) do stamp(preview,merged,x,162,1) end
  board:newCel(orbLayer,f+1,preview,Point(0,0))
  if f==0 then
    merged:saveAs(output..'/lumen-orb-color-cycle.png')
    local first=Image(256,192);first:drawImage(backdrop);first:drawImage(preview)
    local enlarged=Image(768,576);stamp(enlarged,first,0,0,3)
    enlarged:saveAs(output..'/lumen-orb-color-cycle-preview.png')
  end
end
local tag=sprite:newTag(1,40);tag.name='Stationary color cycle'
sprite:saveAs(output..'/lumen-orb-color-cycle.aseprite')
sheet:saveAs(output..'/lumen-orb-color-cycle-sheet.png')
board:saveAs(output..'/lumen-orb-color-cycle-preview.aseprite')
print('Saved fixed-position, 40-frame, four-second color loop: A > B > C > E > D > A.')
