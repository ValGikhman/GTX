;(function ($, window, document) {
    'use strict';
    const form = document.getElementById('pr-batch-inputs');
    if (!form) return;
    const file = document.getElementById('pr-pdf-file'), drop = document.getElementById('pr-pdf-drop');
    const status = document.getElementById('pr-batch-status'), error = document.getElementById('pr-batch-error');
    const run = document.getElementById('pr-batch-run'), stop = document.getElementById('pr-batch-stop');
    const progressModal = document.getElementById('pr-batch-progress-modal');
    const progressMessage = document.getElementById('pr-batch-progress-message');
    const progress = document.getElementById('pr-batch-progress');
    const progressBar = document.getElementById('pr-batch-progress-bar');
    const zip = document.getElementById('pr-batch-zip'), radius = document.getElementById('pr-batch-radius');
    const $grid = $('#pr-batch-grid'), money = new Intl.NumberFormat('en-US', {style:'currency',currency:'USD',maximumFractionDigits:0});
    let rows = [], running = false, parsing = false, stopping = false;
    const token = () => form.querySelector('[name="__RequestVerificationToken"]').value;
    const escape = value => $('<span>').text(value == null ? '' : String(value)).html();
    const number = value => typeof value === 'number' && Number.isFinite(value);
    function showError(message) { error.textContent = message; error.hidden = !message; }
    function busy() {
        file.disabled = parsing || running; run.disabled = parsing || running || !rows.length;
        stop.hidden = !running; stop.disabled = stopping;
        zip.disabled = radius.disabled = parsing || running;
        drop.classList.toggle('is-busy', parsing || running);
        form.setAttribute('aria-busy', String(parsing || running));
    }
    function render() {
        if ($grid.data('add-grid')) { $grid.addGrid('setData', rows, {preserveState:true}); return; }
        $grid.addGrid({data:rows, columns:[
            {field:'Vehicle',title:'Vehicle',width:'24%',filterable:false},
            {field:'Vin',title:'VIN',width:'16%',filterable:false},
            {field:'Miles',title:'Miles',width:'8%',filterable:false,searchable:false,headerClass:'text-end',cellClass:'text-end',template:row => '<span class="pr-batch-odometer">'+(number(row.Miles)?new Intl.NumberFormat('en-US',{maximumFractionDigits:0}).format(row.Miles):'—')+'</span>'},
            {field:'Estimate',title:'Estimate',width:'10%',filterable:false,searchable:false,headerClass:'text-end',cellClass:'text-end',template:row => number(row.Estimate)?money.format(row.Estimate):'—'},
            {field:'Maximum',title:'Max bid',width:'10%',filterable:false,searchable:false,headerClass:'text-end',cellClass:'text-end',template:row => number(row.Maximum)?money.format(row.Maximum):row.Done?'No feasible bid':'—'},
            {field:'Status',title:'Status',width:'22%',filterable:false,headerClass:'text-center',cellClass:'text-center',template:row =>
                `<span class="badge text-bg-${row.StatusTone} mw-100 text-wrap text-break text-center px-3 py-2">${escape(row.Status)}</span>`},
            {field:'Actions',title:'Actions',width:'10%',headerClass:'text-center',cellClass:'text-center',sortable:false,filterable:false,searchable:false,template:row => '<button type="button" class="btn btn-sm pr-batch-retry" data-vin="'+escape(row.Vin)+'" '+(running||parsing?'disabled':'')+'><span class="pr-batch-retry-icon"><i class="bi bi-arrow-clockwise" aria-hidden="true"></i></span><span>Retry</span></button>'}
        ],tableClass:'table align-middle m-0',height:null,pageable:false,resizable:false,reorderable:false,sortable:true,searchable:true,showSearch:true,searchPlaceholder:'Search vehicle, VIN, or status…',showRecordCount:true,recordType:{singular:'vehicle',plural:'vehicles',icon:'bi bi-car-front'},exportToExcel:false,exportToPdf:false,groupable:false,alternateRows:true});
    }
    async function appraise(row) {
        if (!number(row.Miles) || !Number.isInteger(row.Miles) || row.Miles < 0 || row.Miles > 2000000) {
            row.Status = 'Mileage missing or invalid in file; skipped';
            row.StatusTone = 'danger';
            return;
        }
        row.Done=false; row.Estimate=row.Maximum=null;
        row.StatusTone = 'info';
        row.Status='Researching MarketCheck…'; render();
        try {
            const result = await $.ajax({url:form.dataset.appraiseUrl,type:'POST',dataType:'json',timeout:90000,data:{vin:row.Vin,miles:row.Miles,zip:zip.value,radius:radius.value,__RequestVerificationToken:token()}});
            if (!number(result.marketCheckRetailUsd)) throw new Error('No usable MarketCheck estimate returned.');
            row.Estimate=result.marketCheckRetailUsd; row.Maximum=result.maximumHammerBidUsd; row.Done=true;
            row.Status=result.message || 'Complete';
            row.StatusTone = number(row.Maximum) ? 'success' : 'info';
        } catch (failure) {
            const body=failure.responseJSON;
            if (body && number(body.marketCheckRetailUsd)) row.Estimate=body.marketCheckRetailUsd;
            row.Status=body && body.message || failure.message || 'Appraisal failed. Retry this vehicle.';
            row.StatusTone = 'danger';
            if ([401,403,429].includes(failure.status)) { stopping=true; showError(row.Status); }
        }
        render();
    }
    async function start(selected) {
        if (running || parsing || !form.reportValidity()) return;
        const queue = selected || rows.filter(row => !row.Done);
        if (!queue.length) return;
        const modal = window.bootstrap.Modal.getOrCreateInstance(progressModal);
        const previousFocus = document.activeElement;
        running=true; stopping=false; showError(''); busy(); render();
        try {
            status.textContent = '';
            progress.setAttribute('aria-valuenow', '0');
            progressBar.style.width = '0%';
            progressMessage.textContent = 'Starting appraisal…';
            modal.show();
            for (let i=0;i<queue.length && !stopping;i++) {
                progressMessage.textContent = `Appraising ${i + 1} of ${queue.length} · ${queue[i].Vehicle} · ${queue[i].Vin}`;
                await appraise(queue[i]);
                const percent = Math.round((i + 1) * 100 / queue.length);
                progress.setAttribute('aria-valuenow', String(percent));
                progressBar.style.width = `${percent}%`;
            }
        } finally {
            running = false;
            modal.hide();
            busy();
            render();
            const focusTarget = previousFocus && previousFocus.isConnected ? previousFocus : run;
            focusTarget.focus();
        }
    }
    async function upload(selected) {
        if (!selected || running || parsing) return;
        if (!/\.(pdf|csv)$/i.test(selected.name) || selected.size > 10*1024*1024) { showError('Select a PDF or CSV file up to 10 MB.'); return; }
        if (!form.reportValidity()) return;
        parsing=true; showError(''); busy(); status.textContent='Reading '+selected.name+'…';
        try {
            const data=new FormData();data.append('file',selected);data.append('__RequestVerificationToken',token());
            const parsed=await $.ajax({url:form.dataset.parseUrl,type:'POST',data,processData:false,contentType:false,dataType:'json',timeout:45000});
            if (!Array.isArray(parsed.vehicles) || !parsed.vehicles.length) throw new Error('No vehicles were found in this file.');
            rows=parsed.vehicles.map(row=>({Vin:row.Vin,Vehicle:row.Vehicle||'Vehicle · PDF page '+row.Page,Miles:row.Miles,Estimate:null,Maximum:null,Done:false,Status:row.Warning||'Ready',StatusTone:row.Warning?'danger':'info'}));
            render(); status.textContent='';
        } catch (failure) { showError(failure.responseJSON && failure.responseJSON.message || failure.message || 'Could not read this file.'); }
        finally { parsing=false;file.value='';busy(); }

    }
    form.addEventListener('submit',event=>{event.preventDefault();return start();});
    file.addEventListener('change',()=>upload(file.files[0]));
    ['dragenter','dragover'].forEach(name=>drop.addEventListener(name,event=>{event.preventDefault();if(!running&&!parsing)drop.classList.add('is-dragging');}));
    ['dragleave','drop'].forEach(name=>drop.addEventListener(name,event=>{event.preventDefault();drop.classList.remove('is-dragging');}));
    drop.addEventListener('drop',event=>upload(event.dataTransfer.files[0]));
    stop.addEventListener('click',()=>{stopping=true;busy();progressMessage.textContent='Stopping after the current vehicle…';});
    $grid.on('click','.pr-batch-retry',function(){const row=rows.find(row=>row.Vin===this.dataset.vin);if(row)start([row]);});
    [zip,radius].forEach(input=>input.addEventListener('input',()=>{if(running||parsing)return;rows.forEach(row=>{row.Done=false;row.Estimate=row.Maximum=null;row.Status='Ready';row.StatusTone='info';});if(rows.length)render();}));
    document.querySelectorAll('[data-appraisal-mode]').forEach(button=>button.addEventListener('click',()=>{
        const multiple=button.dataset.appraisalMode==='multiple';
        document.getElementById('pr-individual-inputs').hidden=multiple;form.hidden=!multiple;
        document.getElementById('pr-individual-results').hidden=multiple;document.getElementById('pr-batch-results').hidden=!multiple;
        document.querySelectorAll('[data-appraisal-mode]').forEach(tab=>{const selected=tab===button;tab.classList.toggle('active',selected);tab.setAttribute('aria-selected',String(selected));});
        if(multiple&&rows.length)render();
    }));
    window.addEventListener('beforeunload',event=>{if(running){event.preventDefault();event.returnValue='';}});
}(window.jQuery,window,document));
