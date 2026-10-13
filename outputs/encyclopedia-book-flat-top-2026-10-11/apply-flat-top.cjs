const fs=require('fs'), path=require('path');
const sharp=require('C:/Users/nadom/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
(async()=>{
  const evidence=__dirname;
  const name='Inventory Book SpriteSheet.png';
  const before=await sharp(path.join(evidence,'before',name)).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const oldTile=await sharp(path.join(evidence,'book-open-before.png')).ensureAlpha().raw().toBuffer();
  const newTile=await sharp(path.join(evidence,'book-open-candidate.png')).ensureAlpha().raw().toBuffer();
  const meta=fs.readFileSync(path.join(evidence,'before',name+'.meta'),'utf8');
  const rects=new Map([...meta.matchAll(/name: Inventory Book SpriteSheet_(\d+)\s+rect:\s+serializedVersion: \d+\s+x: (\d+)\s+y: (\d+)\s+width: (\d+)\s+height: (\d+)/g)].map(m=>[Number(m[1]),{x:Number(m[2]),y:before.info.height-Number(m[3])-Number(m[5]),width:Number(m[4]),height:Number(m[5])}]));
  const exclusions=new Map([[10,[412,688]],[11,[410,560]],[12,[339,398]]]);
  const frames=[8,9,10,11,12,14,15,16,17];
  const result=Buffer.from(before.data), report=[];
  for(const frame of frames){
    const rect=rects.get(frame); if(!rect||rect.width!==816||rect.height!==720) throw new Error('Unexpected slice '+frame);
    const excluded=exclusions.get(frame);
    let changed=0;
    for(let y=181;y<230;y++) for(let x=85;x<733;x++){
      if(excluded&&x>=excluded[0]&&x<excluded[1]) continue;
      const ti=(y*816+x)*4, si=((rect.y+y)*before.info.width+rect.x+x)*4;
      if(!oldTile.subarray(ti,ti+4).equals(newTile.subarray(ti,ti+4))){
        if(!result.subarray(si,si+4).equals(newTile.subarray(ti,ti+4))) changed++;
        newTile.copy(result,si,ti,ti+4);
      }
    }
    report.push({frame,rect,protectedMovingColumns:excluded||null,changedPixels:changed});
  }
  await sharp(result,{raw:before.info}).png().toFile(path.join(evidence,'Inventory Book SpriteSheet-flat-top.png'));
  fs.writeFileSync(path.join(evidence,'sheet-edit-manifest.json'),JSON.stringify({sheetSize:{width:before.info.width,height:before.info.height},editedBand:{left:85,top:181,right:733,bottom:230},frames:report},null,2));
  process.stdout.write(JSON.stringify({frames:report.length,changedPixels:report.reduce((sum,f)=>sum+f.changedPixels,0)})+'\n');
})().catch(error=>{console.error(error.message);process.exitCode=1;});
