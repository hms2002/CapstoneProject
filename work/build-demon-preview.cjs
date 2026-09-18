const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
function sheet(relative) {
  const file=path.join(root,relative), meta=fs.readFileSync(file+'.meta','utf8');
  const frames={};
  for(const block of meta.split(/- serializedVersion: 2/)) {
    const match=block.match(/name: ([^\r\n]+)\s+rect:\s+serializedVersion: 2\s+x: ([\d.]+)\s+y: ([\d.]+)\s+width: ([\d.]+)\s+height: ([\d.]+)[\s\S]*?pivot: \{x: ([\d.]+), y: ([\d.]+)\}[\s\S]*?internalID: (-?\d+)/);
    if(match)frames[match[8]]={name:match[1],x:+match[2],y:+match[3],w:+match[4],h:+match[5],px:+match[6],py:+match[7]};
  }
  return {url:'data:image/png;base64,'+fs.readFileSync(file).toString('base64'),frames};
}
function sequence(file) {
  return [...fs.readFileSync(path.join(root,file),'utf8').matchAll(/value: \{fileID: (-?\d+), guid:/g)].map(m=>m[1]);
}
const data={body:sheet('Assets/_Project/Art/Sprites/Bosses/DarkLord/DarkLordSheet.png'),effects:sheet('Assets/_Project/Art/Sprites/VFX/Attack/Spell_Projectiles_Sprite_Sheet_2.png'),stockSequence:sequence('Assets/_Project/Art/Animations/VFX/Attack/HomingMagicBaltVFX.anim'),firedSequence:sequence('Assets/_Project/Art/Animations/VFX/Attack/HomingMagicBaltProjectile.anim')};
for(const id of ['7800417542164249327','5718919925475183731'])if(!data.body.frames[id])throw Error('Missing body '+id);
for(const id of [...data.stockSequence,...data.firedSequence])if(!data.effects.frames[id])throw Error('Missing effect '+id);
data.body.frames=Object.fromEntries(['7800417542164249327','5718919925475183731'].map(id=>[id,data.body.frames[id]]));
data.effects.frames=Object.fromEntries([...data.stockSequence,...data.firedSequence].map(id=>[id,data.effects.frames[id]]));
const template=fs.readFileSync(path.join(__dirname,'demon-magic-preview.template.html'),'utf8');
const output=template.replace('__SPRITE_ASSETS__',JSON.stringify(data));
fs.writeFileSync(path.join(__dirname,'demon-magic-preview.html'),output);
console.log(JSON.stringify({bytes:Buffer.byteLength(output),bodyFrames:Object.keys(data.body.frames).length,stockFrames:data.stockSequence.length,firedFrames:data.firedSequence.length}));
