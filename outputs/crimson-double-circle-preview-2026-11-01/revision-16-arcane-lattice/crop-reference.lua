local source=Image{fromFile=app.params.reference}
local crop=Image(128,128,ColorMode.RGB)
crop:drawImage(source,Point(-168,-78))
local sprite=Sprite(128,128,ColorMode.RGB);sprite.cels[1].image=crop
sprite:saveCopyAs(app.params.outputDir..'/reference-02.png');sprite:close()
