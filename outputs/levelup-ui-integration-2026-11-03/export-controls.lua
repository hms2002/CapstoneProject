-- Export approved A artwork without rasterized labels for live TMP binding.
local root,out=app.params.root,app.params.output
local sp=app.open(root..'/outputs/levelup-button-designs-2026-11-03/revision-2-selected-a-title/A-gilt-buttons.aseprite')
sp.layers[3].isVisible=false
local names={'Normal','Focus','Pressed','Disabled'}
for i,name in ipairs(names) do
  local flat=Image(sp.width,sp.height);flat:drawSprite(sp,i)
  local close=Image(160,40);close:drawImage(flat,Point(0,0))
  local reroll=Image(170,40);reroll:drawImage(flat,Point(-175,0))
  close:saveAs(out..'/Close'..name..'.png')
  reroll:saveAs(out..'/Reroll'..name..'.png')
end
sp:saveAs(out..'/LevelRewardControls.aseprite')
sp:close()
print('Approved A sprite layers exported; labels remain live TMP text.')
