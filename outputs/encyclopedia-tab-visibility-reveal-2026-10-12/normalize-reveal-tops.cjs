const fs=require('fs'), path=require('path');
const sharp=require('C:/Users/nadom/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const evidence=__dirname;
const masks=path.join(path.resolve(evidence,'../..'),'Assets/_Project/Art/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/Inventory Book/Book Content Appear/Style 1');

(async()=>{
  const first=await sharp(path.join(evidence,'before/masks/1.png')).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const {width,height,channels}=first.info;
  if(width!==896||height!==720||channels!==4) throw new Error('Unexpected original reveal mask dimensions.');
  const boundaries=[];
  for(const [left,right] of [[138,436],[468,759]]) for(let x=left;x<right;x++) {
    for(let y=0;y<height;y++) if(first.data[(y*width+x)*4+3]>=250) {
      if(y>197) boundaries.push({x,top:y});
      break;
    }
  }
  const records=[];
  for(let frame=1;frame<=18;frame++) {
    const original=await sharp(path.join(evidence,`before/masks/${frame}.png`)).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    if(original.info.width!==width||original.info.height!==height) throw new Error('Unexpected frame size '+frame);
    const pixels=Buffer.from(original.data);
    let added=0;
    for(const {x,top} of boundaries) {
      let boundary=-1;
      // Frame3 shifts its original edge by one row. Do not search deep into revealed holes.
      for(let y=top;y<=top+2;y++) if(original.data[(y*width+x)*4+3]>=250) { boundary=y; break; }
      if(boundary<0) continue;
      const source=(boundary*width+x)*4;
      for(let y=197;y<boundary;y++) {
        const target=(y*width+x)*4;
        if(original.data[target+3]!==0) continue;
        original.data.copy(pixels,target,source,source+4);
        added++;
      }
    }
    const destination=path.join(masks,`${frame}.png`);
    if(added>0) await sharp(pixels,{raw:original.info}).png().toFile(destination);
    records.push({frame,addedOpaquePixels:added,existingNontransparentPixelsPreserved:true});
  }
  fs.writeFileSync(path.join(evidence,'mask-normalization-report.json'),JSON.stringify({size:{width,height},flatTop:197,columnRanges:[[138,436],[468,759]],maxBoundaryLookaheadRows:2,endpointFrame19Preserved:true,records},null,2));
  process.stdout.write(JSON.stringify({editedFrames:records.filter(r=>r.addedOpaquePixels>0).length,addedPixels:records.reduce((n,r)=>n+r.addedOpaquePixels,0)})+'\n');
})().catch(error=>{console.error(error.message);process.exitCode=1;});
