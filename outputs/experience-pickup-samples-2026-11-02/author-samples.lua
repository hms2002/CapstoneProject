-- Native Aseprite pixel authoring. Run with --batch --script-param output=... --script this-file.
local output = assert(app.params.output, 'output directory is required')
local rgba = app.pixelColor.rgba
local function color(hex)
  return rgba(tonumber(hex:sub(1,2),16),tonumber(hex:sub(3,4),16),tonumber(hex:sub(5,6),16),255)
end
local palette = {
  o=color('123F36'), -- deep green silhouette
  s=color('16704C'), -- emerald shadow
  m=color('29AC59'), -- green body
  l=color('75DE68'), -- lime light
  h=color('B5F48A'), -- pale lime facet
  w=color('EDFFD4')  -- mint-white specular
}
local variants = {
  {id='01-jade-orb',name='JADE ORB',rows={
    '....oooo....',
    '..oohhhloo..',
    '.ohwwhllmso.',
    '.owwhllmmso.',
    'ohwhllmmmsso',
    'ohllmmmmmsso',
    'olllmmmmmsso',
    'olmmmmmmssso',
    '.ommmmlmsso.',
    '.osmmllsso..',
    '..oosssso...',
    '....oooo....'
  }},
  {id='02-emerald-shard',name='SHARD',rows={
    '......o......',
    '.....oho.....',
    '....owhlo....',
    '...owhllmo...',
    '..owwhllmso..',
    '.owwhhllmsso.',
    'ohhhlllmsssso',
    '.olllmmsssso.',
    '..olmmmssso..',
    '...olmmsso...',
    '....olmso....',
    '.....oso.....',
    '......o......'
  }},
  {id='03-mana-drop',name='MANA DROP',rows={
    '.......oo...',
    '......olo...',
    '.....ohlo...',
    '.....ohlo...',
    '....ohhlo...',
    '...ohhllmo..',
    '..ohwllmmso.',
    '.ohwwllmmso.',
    '.ohhllmmmsso',
    'ohllmmmmmsso',
    '.olmmmmlmso.',
    '.osmmllmso..',
    '..oosssoo...',
    '....ooo.....'
  }},
  {id='04-star-mote',name='STAR MOTE',rows={
    '......o......',
    '.....olo.....',
    '.....olo.....',
    '....ohhmo....',
    '...ohhhmso...',
    '.oohhwlhmsoo.',
    'olhhwwwllmmso',
    '.oohhwllmsoo.',
    '...olllmso...',
    '....olmso....',
    '.....omo.....',
    '.....oso.....',
    '......o......'
  }},
  {id='05-spirit-seed',name='SPIRIT SEED',rows={
    '.......oooo..',
    '.....oohhlso.',
    '...oohwwllso.',
    '..ohwwhllmso.',
    '.ohwhhllmmso.',
    '.ohhllmllmso.',
    'ohllmmhlmso..',
    'olmmmhllmso..',
    'ommmhllmso...',
    'osmlllmsso...',
    '.osllssoo....',
    '..oooo.......'
  }}
}
-- Dots are transparent cells; each row is an explicitly authored pixel cluster.
local sprites, composites = {}, {}
local function rect(im,x,y,w,h,c)
  for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,c) end end
end
local function stamp(dst,src,x,y,scale)
  for yy=0,src.height-1 do for xx=0,src.width-1 do
    local px=src:getPixel(xx,yy)
    if app.pixelColor.rgbaA(px)>0 then rect(dst,x+xx*scale,y+yy*scale,scale,scale,px) end
  end end
end
for i,v in ipairs(variants) do
  local sprite=Sprite(16,16,ColorMode.RGB)
  sprite.layers[1].name='Body and silhouette'
  local lights=sprite:newLayer(); lights.name='Light and facets'
  local base,light=Image(16,16),Image(16,16)
  local width=0
  for _,row in ipairs(v.rows) do width=math.max(width,#row) end
  local ox,oy=math.floor((16-width)/2),math.floor((16-#v.rows)/2)
  for y,row in ipairs(v.rows) do for x=1,#row do
    local key=row:sub(x,x); local px=palette[key]
    if px then
      local isLight=key=='h' or key=='w'
      base:drawPixel(ox+x-1,oy+y-1,isLight and palette.l or px)
      if isLight then light:drawPixel(ox+x-1,oy+y-1,px) end
    end
  end end
  sprite:newCel(sprite.layers[1],1,base,Point(0,0))
  sprite:newCel(lights,1,light,Point(0,0))
  sprite:saveAs(output..'/'..v.id..'.aseprite')
  local merged=Image(sprite)
  merged:saveAs(output..'/'..v.id..'.png')
  local enlarged=Image(192,192); stamp(enlarged,merged,0,0,12)
  enlarged:saveAs(output..'/'..v.id..'-12x.png')
  sprites[i]=sprite; composites[i]=merged
end
local glyphs={
  A={'010','101','111','101','101'},B={'110','101','110','101','110'},
  C={'011','100','100','100','011'},D={'110','101','101','101','110'},
  E={'111','100','110','100','111'},F={'111','100','110','100','100'},
  G={'011','100','101','101','011'},H={'101','101','111','101','101'},
  I={'111','010','010','010','111'},J={'001','001','001','101','010'},
  K={'101','101','110','101','101'},L={'100','100','100','100','111'},
  M={'101','111','111','101','101'},N={'101','111','111','111','101'},
  O={'010','101','101','101','010'},P={'110','101','110','100','100'},
  Q={'010','101','101','111','011'},R={'110','101','110','101','101'},
  S={'011','100','010','001','110'},T={'111','010','010','010','010'},
  U={'101','101','101','101','111'},V={'101','101','101','101','010'},
  W={'101','101','111','111','101'},X={'101','101','010','101','101'},
  Y={'101','101','010','010','010'},Z={'111','001','010','100','111'},
  ['0']={'111','101','101','101','111'},['1']={'010','110','010','010','111'},
  ['2']={'110','001','010','100','111'},['3']={'110','001','010','001','110'},
  ['4']={'101','101','111','001','001'},['5']={'111','100','110','001','110'},
  ['6']={'011','100','110','101','010'},['/']={'001','001','010','100','100'},
  ['.']={'000','000','000','000','010'}
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
local function centered(im,s,cx,y,c) text(im,s,math.floor(cx-(#s*4-1)/2),y,c) end
local board=Sprite(380,205,ColorMode.RGB)
board.layers[1].name='Comparison layout / not a game asset'
local bg=Image(380,205)
rect(bg,0,0,380,205,color('111C20'))
text(bg,'GREEN / XP',12,12,color('C1F3B1'),2)
text(bg,'FIVE PICKUP STUDIES',221,12,color('849C98'))
text(bg,'16 X 16 / ASEPRITE',221,21,color('849C98'))
rect(bg,12,33,356,1,color('344841'))
for i,v in ipairs(variants) do
  local x=12+(i-1)*72
  rect(bg,x,43,68,132,color('1C2B2E'))
  text(bg,'0'..i,x+6,49,color('91EB79'))
  rect(bg,x+6,112,56,1,color('344841'))
  centered(bg,v.name,x+34,119,color('D4E1D5'))
  rect(bg,x+6,132,56,16,color('111C20'))
  rect(bg,x+6,151,56,16,color('8E8471'))
end
text(bg,'ENLARGED ABOVE / NATIVE PIXELS BELOW',12,183,color('849C98'))
local pi=0
for _,key in ipairs({'o','s','m','l','h','w'}) do
  rect(bg,306+pi*10,181,8,8,palette[key]); pi=pi+1
end
text(bg,'OPAQUE PIXELS / TRANSPARENT BACKGROUND',12,195,color('637D77'))
board:newCel(board.layers[1],1,bg,Point(0,0))
for i,v in ipairs(variants) do
  local x=12+(i-1)*72
  local layer=board:newLayer(); layer.name='0'..i..' '..v.name
  local im=Image(380,205)
  stamp(im,composites[i],x+10,62,3)
  for _,y in ipairs({132,151}) do
    for _,xx in ipairs({8,26,44}) do stamp(im,composites[i],x+xx,y,1) end
  end
  board:newCel(layer,1,im,Point(0,0))
end
board:saveAs(output..'/five-green-pickups.aseprite')
local flattened=Image(board)
flattened:saveAs(output..'/five-green-pickups.png')
local large=Image(1140,615); stamp(large,flattened,0,0,3)
large:saveAs(output..'/five-green-pickups-preview.png')
print('Saved five layered 16x16 sprites, PNG exports, 12x previews, and a native comparison board.')
