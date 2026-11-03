local im=Image{fromFile=app.params.projectDir..'/Assets/_Project/Art/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/Inventory Book SpriteSheet.png'}
local s=Sprite(816,720,ColorMode.RGB)
s.cels[1].image:drawImage(im,Point(-2448,-720))
s:saveCopyAs(app.params.outputDir..'/book-open-before.png');s:close()
