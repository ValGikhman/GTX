const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
function node(extra={}) {return Object.assign({hidden:false,value:'',disabled:false,textContent:'',handlers:{},dataset:{},style:{},classList:{toggle(){},add(){},remove(){}},addEventListener(n,cb){this.handlers[n]=cb;},setAttribute(){},focus(){}},extra);}
async function scenario(failFirst, extension = 'pdf') {
    const nodes={};for(const id of ['pr-batch-inputs','pr-pdf-file','pr-pdf-drop','pr-batch-status','pr-batch-error','pr-batch-run','pr-batch-stop','pr-batch-zip','pr-batch-radius','pr-individual-inputs','pr-individual-results','pr-batch-results'])nodes[id]=node();
    const form=nodes['pr-batch-inputs'];form.dataset={parseUrl:'parse',appraiseUrl:'appraise',cargurusLogo:'/Content/images/cargurus.svg'};form.reportValidity=()=>true;form.querySelector=()=>({value:'token'});
    for (const part of ['title','summary','badge','hint','icon']) nodes['pr-pdf-drop-'+part]=node({hidden:part==='badge'});
    for (const id of ['pr-batch-progress-modal','pr-batch-progress-message','pr-batch-progress','pr-batch-progress-bar']) nodes[id]=node();
    let progressVisible=false;
    const progressSteps=[];
    nodes['pr-batch-zip'].value='45040';nodes['pr-batch-radius'].value='100';
    let rows=[],columns=[],ready=false,active=0,maxActive=0,calls=[];
    const grid={data(){return ready;},addGrid(arg,data){if(typeof arg==='object'){ready=true;rows=arg.data;columns=arg.columns;}else if(arg==='setData')rows=data;return this;},on(){}};
    const $=arg=>arg==='#pr-batch-grid'?grid:{text(v){this.value=v;return this;},html(){return this.value.replaceAll('&','&amp;');}};
    $.ajax=async options=>{
        if (options.url.startsWith('https://www.cargurus.com/')) {
            const url = new URL(options.url);
            if (url.pathname.endsWith('/listMakes.action')) {
                return {success:true,makes:[{name:'Lexus',id:'m37'},{name:'Ford',id:'m2'}]};
            }
            const models = url.searchParams.get('makeId') === 'm37'
                ? [{name:'ES',id:'d2720'}] : [{name:'Transit Cargo',id:'d1067'}];
            return {success:true,models};
        }
        assert.ok(options.url==='parse'||options.data.__RequestVerificationToken==='token');
        if(options.url==='parse')return {vehicles:[{Vin:'58ABZ1B11KU026567',Miles:116589,Vehicle:'2019 Lexus ES 350 Luxury',Year:'2019',Make:'Lexus',Model:'ES 350'},{Vin:'1FTBR3X81MKA59855',Miles:89675,Vehicle:'2021 Ford T250'},{Vin:'JH4CU2F68DC006060',Miles:null,Warning:'Check mileage'}]};
        assert.equal(progressVisible,true);
        assert.match(nodes['pr-batch-progress-message'].textContent,new RegExp(options.data.vin));
        progressSteps.push(nodes['pr-batch-progress-bar'].style.width);
        calls.push(options.data);active++;maxActive=Math.max(maxActive,active);
        await new Promise(resolve=>setTimeout(resolve,1));active--;
        if(failFirst)throw {status:429,responseJSON:{message:'Quota reached'}};
        return {marketCheckRetailUsd:16000,maximumHammerBidUsd:10500,message:'Complete'};
    };
    const window={jQuery:$,bootstrap:{Modal:{getOrCreateInstance(){return {show(){progressVisible=true;},hide(){progressVisible=false;}};}}},addEventListener(){}};const document={getElementById:id=>nodes[id],querySelectorAll:()=>[]};
    vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../Scripts/majordome-appraisal-batch.js'),'utf8'),{window,document,Intl,FormData,URLSearchParams,console});
    nodes['pr-pdf-file'].files=[new File(['%PDF-test'],'all.'+extension,{type:extension==='csv'?'text/csv':'application/pdf'})];
    await nodes['pr-pdf-file'].handlers.change();
    assert.equal(calls.length,0,'Uploading must not trigger appraisals');
    assert.equal(rows.length,3,'Uploading must populate the grid');
    assert.equal(nodes['pr-pdf-drop-title'].textContent,'all.'+extension);
    assert.equal(nodes['pr-pdf-drop-summary'].textContent,'3 vehicles imported');
    assert.equal(nodes['pr-pdf-drop-badge'].hidden,false);
    await nodes['pr-pdf-drop'].handlers.drop({
        preventDefault(){},
        dataTransfer:{files:[new File(['replacement'],'replacement.'+extension)]}
    });
    assert.equal(nodes['pr-pdf-drop-title'].textContent,'replacement.'+extension);
    assert.equal(calls.length,0,'Replacing a file by dropping it must not trigger appraisals');
    const actions = columns.find(column => column.field === 'Actions').template;
    const search = row => new URL(actions(row).match(/href="([^"]+)"/)[1].replaceAll('&amp;', '&'));
    assert.equal(search(rows[0]).href, 'https://www.cars.com/shopping/results/?year_min=2019&year_max=2019&makes%5B%5D=lexus&models%5B%5D=lexus-es_350&zip=45040&maximum_distance=100&sort=list_price&mileage_max=116589');
    assert.equal(search(rows[1]).searchParams.get('models[]'), 'ford-transit_250');
    assert.doesNotMatch(actions(rows[2]), /cars\.com/);
    await new Promise(resolve=>setImmediate(resolve));
    const carGurus = row => [...actions(row).matchAll(/href="([^"]+)"/g)]
        .map(match => new URL(match[1].replaceAll('&amp;', '&')))
        .find(url => url.hostname === 'www.cargurus.com');
    const lexus = carGurus(rows[0]).searchParams;
    assert.equal(lexus.get('startYear'),'2019');
    assert.equal(lexus.get('endYear'),'2019');
    assert.equal(lexus.get('zip'),'45040');
    assert.equal(lexus.get('distance'),'100');
    assert.equal(lexus.get('maxMileage'),'116589');
    assert.equal(lexus.get('sortDirection'),'ASC');
    assert.equal(lexus.get('sortType'),'PRICE');
    assert.equal(lexus.get('makeModelTrimPaths').split(',').length,8);
    assert.match(lexus.get('makeModelTrimPaths'),/m37\/d2720\/350 FWD/);
    assert.equal(carGurus(rows[1]).searchParams.get('makeModelTrimPaths'),'m2/d1067');
    assert.equal(carGurus(rows[2]),undefined);
    await form.handlers.submit({preventDefault(){}});
    assert.equal(progressVisible,false);
    assert.deepEqual(progressSteps,failFirst?['0%']:['0%','33%']);
    assert.equal(nodes['pr-batch-progress-bar'].style.width,failFirst?'33%':'100%');
    assert.equal(maxActive,1,'Requests must run sequentially');assert.equal(nodes['pr-batch-run'].disabled,false);
    if(failFirst){assert.equal(calls.length,1);assert.equal(nodes['pr-batch-error'].textContent,'Quota reached');assert.equal(rows[0].Done,false);}
    else {assert.equal(calls.length,2);assert.equal(calls[0].miles,116589);assert.equal(calls[1].miles,89675);assert.equal(rows[0].Estimate,16000);assert.equal(rows[0].Maximum,10500);assert.equal(rows[1].Done,true);assert.equal(rows[2].Status,'Mileage missing or invalid in file; skipped');}
}
(async()=>{await scenario(false);await scenario(false,'csv');await scenario(true,'csv');console.log('PASS: PDF and CSV parse-only uploads, explicit Appraise all, serial queue, exact mileages, estimates/bids, missing mileage skip and quota-stop behavior.');})().catch(e=>{console.error(e);process.exitCode=1;});
