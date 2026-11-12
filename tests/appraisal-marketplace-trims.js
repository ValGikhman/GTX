const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../Scripts/majordome-appraisal-batch.js'), 'utf8');
const context = vm.createContext({
    URLSearchParams,
    zip: { value: '45011' },
    radius: { value: '100' },
    carGurusEntities: async makeId => makeId
        ? [{ id: 'd2512', name: 'Stelvio' }]
        : [{ id: 'm124', name: 'Alfa Romeo' }]
});

// Run the actual identity and link builders, with an offline CarGurus catalog.
vm.runInContext(source.match(/const vehicleSlug = [^\n]+/)[0], context);
vm.runInContext(source.slice(source.indexOf('    function searchVehicle(row)'), source.indexOf('    function decodedLabel(row)')), context);
vm.runInContext(source.slice(source.indexOf('    function carsDrivetrain(driveType)'), source.indexOf('    function carGurusEntities(makeId)')), context);
vm.runInContext(source.slice(source.indexOf('    async function findCarGurusPaths(row)'), source.indexOf('    function renderActions(row)')), context);

function row(trim = 'Veloce', drive = 'AWD') {
    return {
        Vin: 'ZASPAKBN9P7D56780', Miles: 26124,
        DecodedVehicle: {
            Year: '2023', Make: 'Alfa Romeo', Model: 'Stelvio',
            Style: 'AWD Veloce 4dr Crossover', BodyStyle: 'Wagon', Trim: trim, DriveType: drive
        }
    };
}

(async () => {
    for (const trim of ['Veloce', 'veloce']) {
        const vehicle = row(trim);
        await context.findCarGurusPaths(vehicle);
        const url = new URL(context.carGurusSearchUrl(vehicle));
        assert.equal(url.hostname, 'www.cargurus.com');
        assert.equal(url.searchParams.get('makeModelTrimPaths'), 'm124/d2512/Veloce AWD');
        assert.equal(url.searchParams.get('startYear'), '2023');
        assert.equal(url.searchParams.get('endYear'), '2023');
        assert.equal(url.searchParams.get('zip'), '45011');
        assert.equal(url.searchParams.get('distance'), '100');
        assert.equal(url.searchParams.get('maxMileage'), '26124');
        assert.equal(url.searchParams.get('sortType'), 'PRICE');
        assert.equal(url.searchParams.get('sortDirection'), 'ASC');
        const cars = new URL(context.carsSearchUrl(vehicle));
        assert.equal(cars.hostname, 'www.cars.com');
        assert.equal(cars.searchParams.get('stock_type'), 'used');
        assert.equal(cars.searchParams.get('makes[]'), 'alfa_romeo');
        assert.equal(cars.searchParams.get('models[]'), 'alfa_romeo-stelvio');
        assert.equal(cars.searchParams.get('trims[]'), 'alfa_romeo-stelvio-veloce');
        assert.equal(cars.searchParams.has('body_style_slugs[]'), false, 'Do not send body style, including a decoded Wagon classification');
        assert.equal(cars.searchParams.get('drivetrain_slugs[]'), 'all_wheel_drive');
        assert.equal(cars.searchParams.get('year_min'), '2023');
        assert.equal(cars.searchParams.get('year_max'), '2023');
        assert.equal(cars.searchParams.get('zip'), '45011');
        assert.equal(cars.searchParams.get('maximum_distance'), '100');
        assert.equal(cars.searchParams.get('mileage_max'), '26124');
        assert.equal(cars.searchParams.get('sort'), 'list_price');
    }
    for (const vehicle of [row('', 'AWD'), row('Veloce', ''), row('Veloce', 'RWD')]) {
        await context.findCarGurusPaths(vehicle);
        assert.equal(vehicle.CarGurusPaths, 'm124/d2512', 'Do not guess a trim when decoded values do not support it');
    }
    for (const [decoded, expected] of [['AWD', 'all_wheel_drive'], ['Front-wheel Drive', 'front_wheel_drive'], ['RWD', 'rear_wheel_drive'], ['4WD', 'four_wheel_drive'], ['4x2', null], ['', null]]) {
        assert.equal(context.carsDrivetrain(decoded), expected);
    }
    console.log('PASS: Cars.com and CarGurus decoded Stelvio Veloce AWD filters, year/mileage/location scope and ambiguity handling.');
})().catch(error => { console.error(error); process.exitCode = 1; });
