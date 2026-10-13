"""Read-only integration/source-alpha preservation evidence. Writes only its JSON report."""
from pathlib import Path
import hashlib, json, re, sys, subprocess
import numpy as np
from PIL import Image

EVIDENCE=Path(__file__).resolve().parent
ROOT=EVIDENCE.parent.parent
BEFORE=EVIDENCE/'before'
PREFAB=ROOT/'Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab'
MASKS=ROOT/'Assets/_Project/Art/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/Inventory Book/Book Content Appear/Style 1'
REPORT=EVIDENCE/'integration-preservation-report.json'
BOOK='5501019934593851225'
ITEM_RIGHT='4296879548229141479'
ORDER=['2303885753161862740','7655396100115582861','7902588248173237715','2969352487574867985','6851858236227250328','5521546214420382048']
HEADER=re.compile(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n',re.M)
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()
def records(p):
    s=Path(p).read_text(encoding='utf-8-sig');ms=list(HEADER.finditer(s));d={};order=[]
    for n,m in enumerate(ms):
        ident=m[2]
        if ident in d:raise ValueError('Duplicate ID '+ident)
        d[ident]={'class':m[1],'text':s[m.start():ms[n+1].start() if n+1<len(ms) else len(s)]};order.append(ident)
    return s[:ms[0].start()],order,d
def field_block(r,name):
    lines=r['text'].splitlines()
    for n,l in enumerate(lines):
        if not l.startswith('  '+name+':'):continue
        end=n+1
        while end<len(lines) and not re.match(r'^  [^ -][^:]*:',lines[end]):end+=1
        return '\n'.join(lines[n:end])
    return None
def without_block(r,name):
    block=field_block(r,name)
    return '\n'.join(r['text'].splitlines()).replace(block,'') if block else '\n'.join(r['text'].splitlines())
def field(r,name):
    m=re.search(r'^  '+re.escape(name)+r':\s*([^\n]*)$',r['text'],re.M)
    if not m:raise ValueError('Missing field '+name)
    return m[1].strip()
def vec(r,name):
    v=dict(re.findall(r'([xy]):\s*([-+\d.eE]+)',field(r,name)))
    return [float(v['x']),float(v['y'])]
def by_identifier(d,ident):
    ids=[k for k,r in d.items() if field_block(r,'m_EditorClassIdentifier')=='  m_EditorClassIdentifier: '+ident]
    if len(ids)!=1:raise ValueError('Expected one '+ident)
    return ids[0]
def presentation(d):
    result={}
    for k,r in d.items():
        names=re.findall(r'^  ([^ :]+):',r['text'],re.M)
        p={n:field_block(r,n) for n in names if re.search(r'font|sprite|aspect',n,re.I) or n in ('m_Material','m_sharedMaterial')}
        if p:result[k]=p
    return result
def normalized_refs(d):
    out={}
    for k,r in d.items():
        text=without_block(r,'m_Children') if k==BOOK else r['text']
        if k==ITEM_RIGHT:text=re.sub(r'^  pageContentRoot:.*\n','',text,flags=re.M)
        out[k]=[l for l in text.splitlines() if re.search(r'\b(?:fileID|guid):',l)]
    return out
def inspect_hashes(manifest):
    entries=json.loads(manifest.read_text(encoding='utf-8-sig'))
    return [{'path':x['Path'],'expected_sha256':x['Hash'].upper(),'actual_sha256':sha(x['Path']),
             'unchanged':sha(x['Path'])==x['Hash'].upper()} for x in entries]
def alpha_metric(alpha,rect):
    x,y,w,h=rect;s=1480/720
    xs=np.arange(x+.25,x+w,.5)[None,:];ys=np.arange(y+.25,y+h,.5)[:,None]
    xx=np.floor(xs/s+448).astype(int);yy=np.floor(360-ys/s).astype(int)
    xx,yy=np.broadcast_arrays(xx,yy);a=alpha[yy,xx];miss=a<250
    return {'samples':int(a.size),'uncovered_samples':int(miss.sum()),'uncovered_ui_area':round(float(miss.sum())*.25,3),
            'uncovered_percent':round(float(miss.mean())*100,6),'uncovered_top_edge_percent':round(float(miss[-1].mean())*100,6)}
def main():
    report={'passed':False,'purpose':'read-only scoped integration and alpha preservation evidence','checks':[],
            'limits':['Source-only checks; no Unity compilation, import, PlayMode test or actual game render is performed by this script.',
                      'Individual Item definition asset bytes have no task-start hash manifest; no byte-preservation claim is made for them.',
                      'Animation files are additionally compared with tracked Git HEAD, not an invented task-start file snapshot.']}
    def check(name,ok,**details):report['checks'].append({'name':name,'passed':bool(ok),**details})
    try:
        op,oo,old=records(BEFORE/'EncyclopediaUI.prefab');nprefix,no,new=records(PREFAB)
        common=set(old)&set(new);changed=sorted(k for k in common if old[k]['text']!=new[k]['text'])
        report['prefab_sha256']=sha(PREFAB);report['prefab_before_sha256']=sha(BEFORE/'EncyclopediaUI.prefab')
        check('2583_same_record_ids_classes_and_record_order',len(old)==len(new)==2583 and set(old)==set(new) and oo==no and op==nprefix
              and all(old[k]['class']==new[k]['class'] for k in common),before_count=len(old),current_count=len(new),changed_record_ids=changed)
        check('only_item_right_reference_and_book_child_order_changed',set(changed)=={BOOK,ITEM_RIGHT},unchanged_record_count=len(common)-len(changed))
        children=re.findall(r'fileID: (-?\d+)',field_block(new[BOOK],'m_Children'))
        check('book_children_only_reordered_and_page_cover_above_pages_below_full_view',children==ORDER
              and sorted(children)==sorted(re.findall(r'fileID: (-?\d+)',field_block(old[BOOK],'m_Children')))
              and without_block(old[BOOK],'m_Children')==without_block(new[BOOK],'m_Children'),current_children=children)
        stripped='\n'.join(l for l in new[ITEM_RIGHT]['text'].splitlines() if not l.startswith('  pageContentRoot:'))
        check('item_right_only_page_content_root_added',stripped=='\n'.join(old[ITEM_RIGHT]['text'].splitlines())
              and field(new[ITEM_RIGHT],'pageContentRoot')=='{fileID: 3864464956366859789}')
        root_rect=next(k for k,r in new.items() if r['class']=='224' and field_block(r,'m_GameObject')=='  m_GameObject: {fileID: 3864464956366859789}')
        check('page_content_root_is_existing_item_detail_scroll_subtree',field(new['3864464956366859789'],'m_Name')=='ItemDetailPanel'
              and field(new[root_rect],'m_Father')=='{fileID: 7655396100115582861}'
              and field(new['3791403012329970838'],'m_GameObject')=='{fileID: 3864464956366859789}'
              and root_rect!=next(k for k,r in new.items() if r['class']=='224' and field_block(r,'m_GameObject')=='  m_GameObject: {fileID: 3675305509070163650}'),root_rect_id=root_rect)
        allowed=(EVIDENCE/'authoring-allowed-ids.txt').read_text(encoding='utf-8-sig').split()
        check('authoring_touch_list_matches_two_allowed_records',set(allowed)=={BOOK,ITEM_RIGHT} and len(allowed)==2)
        check('all_existing_fileid_guid_references_preserved_except_approved_new_root_and_order',normalized_refs(old)==normalized_refs(new))
        pres=presentation(old)
        check('2964_font_sprite_material_aspect_fields_preserved',pres==presentation(new) and sum(map(len,pres.values()))==2964,
              inspected_record_count=len(pres),inspected_field_count=sum(map(len,pres.values())))
        illustration=new['3202883555615465603']
        check('illustration_594_389_y_minus_7_and_all_geometry_preserved',vec(illustration,'m_SizeDelta')==[594.,389.]
              and vec(illustration,'m_AnchoredPosition')==[0.,-7.]
              and all(old[k]['text']==new[k]['text'] for k in common if old[k]['class']=='224' and k!=BOOK))
        item=by_identifier(new,'Assembly-CSharp::EncyclopediaItemTab')
        preset_names=('weaponTitle','relicTitle','consumableTitle','itemDatabase')
        check('three_item_title_presets_and_database_reference_preserved',all(field_block(old[item],n)==field_block(new[item],n) for n in preset_names),field_names=list(preset_names))
        protected=inspect_hashes(EVIDENCE/'protected-before-hashes.json')
        check('eight_protected_scene_catalog_book_art_and_creature_runtime_hashes_preserved',len(protected)==8 and all(x['unchanged'] for x in protected),files=protected)
        check('book_presentation_source_matches_task_start',sha(BEFORE/'EncyclopediaBookPresentation.cs')==sha(ROOT/'Assets/_Project/Runtime/UI/Encyclopedia/EncyclopediaBookPresentation.cs'))
        masks_protected=inspect_hashes(EVIDENCE/'protected-mask-hashes.json')
        check('56_mask_metadata_unused_frames_endpoint_and_sheet_hashes_preserved',len(masks_protected)==56 and all(x['unchanged'] for x in masks_protected),files=masks_protected)
        mask_names={Path(x['path']).name for x in masks_protected}
        check('all_36_png_metas_and_19_to_36_pngs_have_protected_hashes',all(f'{n}.png.meta' in mask_names for n in range(1,37)) and all(f'{n}.png' in mask_names for n in range(19,37)))
        animation_files=[]
        for rel in ['Assets/_Project/Art/Animations/Encyclopedia/UIBook/ENC_UIBook_ContentAppear.anim',
                    'Assets/_Project/Art/Animations/Encyclopedia/UIBook/AC_Encyclopedia_PageCover.controller',
                    'Assets/_Project/Art/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/Book.controller']:
            for rp in (rel,rel+'.meta'):
                result=subprocess.run(['git','show','HEAD:'+rp],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
                # Git stores LF while working assets may use CRLF; compare text, not line-ending storage.
                identical=result.returncode==0 and result.stdout.decode('utf-8-sig').replace('\r\n','\n')==(ROOT/rp).read_text(encoding='utf-8-sig')
                animation_files.append({'path':rp,'tracked_head_text_unchanged':identical})
        check('content_clip_controllers_and_metadata_match_tracked_head',all(x['tracked_head_text_unchanged'] for x in animation_files),files=animation_files)
        clip=(ROOT/'Assets/_Project/Art/Animations/Encyclopedia/UIBook/ENC_UIBook_ContentAppear.anim').read_text()
        check('content_clip_0_4_seconds_50_hz_and_sprite_only_timing_retained','  m_SampleRate: 50\n' in clip and '    m_StopTime: 0.4\n' in clip and '  m_FloatCurves: []\n' in clip and '    attribute: m_Sprite\n' in clip)
        base=np.array(Image.open(BEFORE/'masks/1.png').convert('RGBA'))
        boundaries={}
        allowed_gap=np.zeros(base.shape[:2],dtype=bool)
        for lo,hi in ((138,436),(468,759)):
            for x in range(lo,hi):
                ys=np.flatnonzero(base[:,x,3]>=250)
                if len(ys) and ys[0]>197:
                    boundaries[x]=int(ys[0]);allowed_gap[197:int(ys[0])+2,x]=True
        frame_checks=[]
        arrays=[]
        for n in range(1,20):
            prior=np.array(Image.open(BEFORE/f'masks/{n}.png').convert('RGBA'))
            cur=np.array(Image.open(MASKS/f'{n}.png').convert('RGBA'));arrays.append(cur)
            same_shape=prior.shape==cur.shape==(720,896,4)
            delta=np.any(prior!=cur,axis=2);old_opaque=prior[:,:,3]>0
            expected=prior.copy()
            for x,top in boundaries.items():
                opaque=np.flatnonzero(prior[top:top+3,x,3]>=250)
                if not len(opaque):continue
                boundary=top+int(opaque[0])
                for y in range(197,boundary):
                    if prior[y,x,3]==0:expected[y,x]=prior[boundary,x]
            old_colors={tuple(c) for c in np.unique(prior[old_opaque],axis=0)}
            added_colors={tuple(c) for c in np.unique(cur[delta],axis=0)}
            frame_checks.append({'frame':n,'dimensions_896_720':same_shape,'changed_pixels':int(delta.sum()),
                'all_old_nontransparent_rgba_pixels_preserved':bool(np.array_equal(prior[old_opaque],cur[old_opaque])),
                'only_original_round_gap_changes':bool(not np.any(delta&~allowed_gap)),
                'only_transparent_to_opaque_additions':bool(not np.any(delta&((prior[:,:,3]!=0)|(cur[:,:,3]<250)))),
                'current_boundary_color_and_alpha_inherited_with_2_row_lookup':bool(np.array_equal(cur,expected)),
                'new_palette_colors':len(added_colors-old_colors),
                'alpha_pattern_outside_original_round_gap_identical':bool(np.array_equal(prior[:,:,3][~allowed_gap],cur[:,:,3][~allowed_gap]))})
        report['mask_frames']=frame_checks
        check('19_masks_dimensions_and_old_nontransparent_pixels_preserved',all(x['dimensions_896_720'] and x['all_old_nontransparent_rgba_pixels_preserved'] for x in frame_checks))
        check('18_masks_only_approved_gap_additions_and_no_new_palette_colors',all(x['only_original_round_gap_changes'] and x['only_transparent_to_opaque_additions'] and x['new_palette_colors']==0 for x in frame_checks),changed_frames=[x['frame'] for x in frame_checks if x['changed_pixels']],added_pixels=sum(x['changed_pixels'] for x in frame_checks))
        check('all_frames_match_original_reveal_boundary_profile_including_frame3_shift',all(x['current_boundary_color_and_alpha_inherited_with_2_row_lookup'] and x['alpha_pattern_outside_original_round_gap_identical'] for x in frame_checks))
        prior_endpoint=np.array(Image.open(BEFORE/'masks/19.png').convert('RGBA'))
        endpoint_count=int(np.count_nonzero(arrays[18][:,:,3]))
        check('late_16_to_19_existing_reveal_progression_and_endpoint_pixels_preserved',all(frame_checks[n-1]['current_boundary_color_and_alpha_inherited_with_2_row_lookup'] and frame_checks[n-1]['alpha_pattern_outside_original_round_gap_identical'] for n in range(16,20)) and np.array_equal(prior_endpoint,arrays[18]),endpoint_nontransparent_pixels=endpoint_count,baseline_endpoint_nontransparent_pixels=int(np.count_nonzero(prior_endpoint[:,:,3])),explanation='Frame19 already contains 15 nontransparent pixels. They remain byte-for-byte unchanged; the existing BookPresentation hides the cover after sampling completes.')
        targets={'illustration':[43.,-56.,594.,389.],'creature_left_title':[-615.,263.,550.,52.],'monster_title':[65.,263.,550.,52.],'monster_preview':[65.,-5.,550.,250.],'left_footer':[-430.,-400.,180.,50.]}
        metrics={k:alpha_metric(arrays[0][:,:,3],r) for k,r in targets.items()}
        check('first_normalized_mask_fully_covers_expanded_illustration_titles_preview_and_footer',all(x['uncovered_samples']==0 for x in metrics.values()),metrics=metrics)
        normalized=json.loads((EVIDENCE/'mask-normalization-report.json').read_text())
        check('independent_pixel_delta_matches_normalizer_report',sum(x['changed_pixels'] for x in frame_checks)==sum(x['addedOpaquePixels'] for x in normalized['records'])==37075)
        post=json.loads((EVIDENCE/'prefab-after-authoring-hash.json').read_text(encoding='utf-8-sig'))
        check('prefab_bytes_match_post_authoring_snapshot',sha(PREFAB)==post['Hash'].upper())
        report['passed']=all(x['passed'] for x in report['checks'])
    except Exception as exc:
        report['error']=f'{type(exc).__name__}: {exc}'
    REPORT.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'passed':report['passed'],'checks':len(report['checks']),'failed':[x['name'] for x in report['checks'] if not x['passed']],'error':report.get('error'),'report':str(REPORT)},ensure_ascii=False))
    return 0 if report['passed'] else 1
if __name__=='__main__':sys.exit(main())
