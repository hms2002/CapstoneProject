-- Native 48px production counterpart of the approved violet translation button.
-- Extends the project's reproducible Aseprite UI authoring pipeline.
local out = assert(app.params.outputDir)
local pc = app.pixelColor
local function color(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local function rect(im,x,y,w,h,c)
  for j=y,y+h-1 do for i=x,x+w-1 do im:drawPixel(i,j,color(c)) end end
end
local glyph = Image(48,48,ColorMode.RGB)
-- 文 at upper left; diagonal strokes use regular two-pixel clusters.
rect(glyph,20,9,3,4,'eee2ff'); rect(glyph,11,14,23,3,'eee2ff')
for i=0,7 do
  rect(glyph,16+i,18+i*2,3,2,'eee2ff')
  rect(glyph,27-i,18+i*2,3,2,'eee2ff')
end
-- A at lower right, separate foreground glyph.
for y=22,37 do
  local d=math.floor((y-22)/2)
  rect(glyph,32-d,y,3,1,'eee2ff'); rect(glyph,33+d,y,3,1,'eee2ff')
end
rect(glyph,28,31,12,3,'eee2ff')
local sheet=Sprite(144,48,ColorMode.RGB)
for s=0,2 do
  local im=Image(48,48,ColorMode.RGB)
  local fill=s==1 and '605087' or (s==2 and '332947' or '41365e')
  local edge=s==1 and 'd3b9fa' or (s==2 and '88759f' or 'a18bc4')
  rect(im,0,0,48,48,'191326'); rect(im,1,1,46,46,edge)
  rect(im,3,3,42,42,'241c36'); rect(im,4,4,40,40,fill)
  rect(im,4,43,40,2,'302641'); rect(im,43,4,2,40,'302641')
  for _,p in ipairs({{1,1},{41,1},{1,41},{41,41}}) do
    rect(im,p[1],p[2],6,6,edge); rect(im,p[1]+1,p[2]+1,4,1,'d7c2f2')
    rect(im,p[1]+4,p[2]+3,1,2,'65517e')
  end
  -- A one-pixel dark contour separates the symbol from the violet face.
  for y=1,46 do for x=1,46 do
    if pc.rgbaA(glyph:getPixel(x,y))>0 then
      for dy=-1,1 do for dx=-1,1 do im:drawPixel(x+dx,y+dy,color('21172f')) end end
    end
  end end
  im:drawImage(glyph)
  local layer=s==0 and sheet.layers[1] or sheet:newLayer()
  layer.name=({'Normal','Hover','Pressed'})[s+1]
  sheet:newCel(layer,1,im,Point(s*48,0))
  -- Each state is also exported individually, with no runtime slicing.
  local state=Sprite(48,48,ColorMode.RGB)
  state:newCel(state.layers[1],1,im)
  state:saveCopyAs(out..'/TitleLanguage'..({'Normal','Hover','Pressed'})[s+1]..'.png')
  state:close()
end
sheet:saveAs(out..'/TitleLanguageButton.aseprite')
sheet:close()
