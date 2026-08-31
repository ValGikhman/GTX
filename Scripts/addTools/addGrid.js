/*!
 * Dependencies: jQuery 3+, Bootstrap Icons (bi-*)
 *
 * Usage:
 *   $('#myContainer').addGrid({ url: '/api/data', columns: [...] });
 *   $('#myContainer').addGrid('refresh');
 *   var item = $('#myContainer').addGrid('getDataItem', trElement);
 *
 * See README.md in this folder for full documentation.
 */
;(function ($) {
    'use strict';

    const DATA_KEY = 'add-grid';
    const MIN_RESIZABLE_COLUMN_WIDTH = 50;

    function debounce(fn, wait) {
        let timer;
        return function () {
            const ctx = this, args = arguments;
            clearTimeout(timer);
            timer = setTimeout(function () { fn.apply(ctx, args); }, wait);
        };
    }

    function emptyStateMarkup(text, hint) {
        return '<span class="pg-empty-content">'
            + '<span class="pg-empty-visual" aria-hidden="true">'
            + '<span class="pg-empty-card-back"></span>'
            + '<span class="pg-empty-icon">'
            + '<svg viewBox="0 0 36 36" focusable="false">'
            + '<rect x="5" y="6" width="22" height="20" rx="3" />'
            + '<path d="M5 12h22M11 12v14M15 16h7M15 20h5" />'
            + '<circle class="pg-empty-icon-accent" cx="25" cy="25" r="6" />'
            + '<path class="pg-empty-icon-lens" d="m29.5 29.5 3 3" />'
            + '</svg>'
            + '</span>'
            + '</span>'
            + '<span class="pg-empty-copy">'
            + '<span class="pg-empty-text">' + String(text || 'No records yet') + '</span>'
            + '<span class="pg-empty-hint">' + String(hint || 'New activity will appear here.') + '</span>'
            + '</span>'
            + '</span>';
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Default options
    // ════════════════════════════════════════════════════════════════════════
    const defaults = {
        /** AJAX URL to fetch data. Mutually exclusive with `data`. */
        url: null,

        /** Static data array. When provided, `url` is ignored. */
        data: null,

        /**
         * Schema to extract the records array from the AJAX response.
         * @example { data: function(resp) { return resp.Data; } }
         */
        schema: null,

        /** Number of rows per page. */
        pageSize: 50,

        /** Enable client-side pagination and render the grid footer/pager. */
        pageable: true,

        /** Show a filter/search-sensitive row total when pagination is disabled. */
        showRecordCount: false,

        /**
         * Language and Bootstrap Icon used by the filter-sensitive footer total.
         * Override this per grid so the footer describes the records it contains.
         * @example { singular: 'vehicle', plural: 'vehicles', icon: 'bi bi-car-front-fill' }
         */
        recordType: {
            singular: 'row',
            plural: 'rows',
            icon: 'bi bi-list-ul',
            emptyText: null
        },

        /**
         * Optional filter-sensitive compound footer summary.
         * Receives the currently matched records and total, and returns segments:
         * [{ count, singular, plural, icon, tone }]. The first segment is primary;
         * later segments are displayed after a plus sign.
         */
        recordSummary: null,

        /**
         * Initial sort state.
         * @example [{ field: 'Name', dir: 'asc' }]
         */
        defaultSort: [],

        /**
         * Column definitions.
         * Each column: { field, title, width, hidden, sortable, filterable, searchable, encoded,
         *                resizable, reorderable, autozoomable, headerClass, cellClass, template }
         *   - field      {string}  Property name on the data object (required)
         *   - title      {string}  Header label (defaults to field)
         *   - width      {number|string} min-width in px or CSS string
         *   - hidden     {boolean} Exclude column from rendering (default false)
         *   - sortable   {boolean} Allow sort on this column (default true)
         *   - filterable {boolean} Allow filter on this column (default true)
         *   - searchable {boolean} Include in quick-search and highlight matches (default true)
         *   - encoded    {boolean} false = render cell value as raw HTML (default true)
         *   - resizable  {boolean} Inherits grid-level `resizable`; set false to disable per column
         *   - reorderable {boolean} Inherits grid-level `reorderable`; set false to keep the column fixed
         *   - headerClass {string|function} CSS class(es) applied to the generated <th>
         *   - cellClass {string|function} CSS class(es) applied to generated <td> cells
         *   - sortField/filterField/searchField {string} Alternate data properties for those operations
         *   - sortValue/filterValue/searchValue {function} Custom value resolver; takes precedence over *Field
         *   - autozoomable {boolean} Inherits grid-level `autozoomable`; shows a full-value popup when ellipsized
         *   - template   {string}  CSS selector for a <script type="text/x-addgrid-template"> element.
         *                          The template HTML is stamped per row; use {{FieldName}} tokens to
         *                          interpolate any data field. Implies encoded: false. Defaults
         *                          searchable to false (set searchable: true to search the field value).
         *                          Example: template: '#myActionTemplate'
         *
         * Special field name: 'Selection' renders a header select-all checkbox.
         */
        columns: [],

        emptyText: 'No records yet',
        emptyHint: 'New activity will appear here.',

        /** Enable sorting and filtering globally; columns can opt out individually. */
        sortable: true,
        filterable: true,

        /** Optional generated table/tbody identifiers and CSS classes. */
        tableId: null,
        tableClass: '',
        tbodyId: null,
        tbodyClass: '',

        /** CSS classes and HTML attributes applied to each generated data row. */
        rowClass: null,
        rowAttributes: null,

        /** Optional detail/subgrid row rendered immediately after each data row. */
        detailTemplate: null,
        detailExpanded: null,
        detailRowId: null,
        detailRowClass: '',
        detailRowAttributes: null,
        detailCellClass: '',

        /** Extra class for filter popups appended to document.body. */
        filterDropdownClass: '',

        /**
         * Allow multiple rows to be selected simultaneously.
         * When false, selecting a new row deselects the previous one.
         */
        multiSelect: false,

        /**
         * CSS height of the grid wrapper element.
         * Set to null for auto height (no scroll container).
         */
        height: '85vh',

        /**
         * Show a popup with the full cell value when hovering an ellipsized cell.
         * Default false. Can be overridden per column with `autozoomable`.
         */
        autozoomable: false,

        /**
         * Show column resize handles in the header.
         * Resizing changes only the active column and keeps adjacent columns fixed.
         * Default false.
         */
        resizable: true,

        // ── Event callbacks ──────────────────────────────────────────────────
        // Each callback also fires as a jQuery event on the container element.
        // jQuery event name pattern: 'addgrid:<eventname>'
        //   e.g.  $('#el').on('addgrid:rowclick', function(e, data) { ... });

        /** Fires before the AJAX request is sent. () */
        onBeforeLoad: null,

        /** Fires after data is fetched and rendered. ({ data, total }) */
        onDataBound: null,

        /** Fires after any body render, including sort/filter/search/reorder. ({ data, total }) */
        onRender: null,

        /** Fires when a row is clicked. ({ dataItem, rowElement, event }) */
        onRowClick: null,

        /** Fires when a row is double-clicked. ({ dataItem, rowElement, event }) */
        onRowDblClick: null,

        /** Fires on right-click of a row. ({ dataItem, rowElement, event }) */
        onRowContextMenu: null,

        /** Fires when a cell is clicked. ({ dataItem, field, value, cellElement, rowElement, columnIndex, event }) */
        onCellClick: null,

        /** Fires when a cell is double-clicked. ({ dataItem, field, value, cellElement, rowElement, columnIndex, event }) */
        onCellDblClick: null,

        /** Fires when a row transitions to selected. ({ dataItem, rowElement }) */
        onRowSelect: null,

        /** Fires when a row transitions to deselected. ({ dataItem, rowElement }) */
        onRowDeselect: null,

        /** Fires whenever the selection set changes. ({ selected: [...dataItems] }) */
        onSelectionChange: null,

        /** Fires after sort changes. ({ sort: [{ field, dir }] }) */
        onSortChange: null,

        /** Fires after filters are applied. ({ filters: { field: [value, ...] } }) */
        onFilterChange: null,

        /** Fires when the user navigates to a new page. ({ page, pageSize, total }) */
        onPageChange: null,

        /**
         * Show the quick-search bar above the grid.
         * Default true. Set false to hide it entirely.
         */
        searchable: true,
        showSearch: true,

        /** Initial quick-search value, input placeholder, and optional row-level matcher. */
        searchTerm: '',
        searchPlaceholder: 'Search…',
        searchPredicate: null,

        /** Fires when the search term changes. ({ term, matchCount }) */
        onSearchChange: null,

        /**
         * Alternate row background colours using the pg-row-even / pg-row-odd classes.
         * Set false for a flat (no-stripe) grid.
         * Default true.
         */
        alternateRows: true,

        /**
         * Show active-filter chips bar below the search bar when filters are applied.
         * Each chip labels the column and active values; clicking × removes that filter.
         * A "Clear all" button appears when any chip is present.
         * Default true.
         */
        showFilterChips: true,

        /**
         * Show an "Export to Excel" button in the toolbar.
         * Exports all currently filtered/searched/sorted rows as a UTF-8 CSV file.
         * Default true. Set false to hide it.
         */
        exportToExcel: true,

        /**
         * Additional CSS class(es) appended to the Export to Excel button.
         * The button already has `btn btn-sm btn-outline-success`.
         * @example exportToExcelClass: 'ms-2'
         */
        exportToExcelClass: '',

        /**
         * Base file name (without extension) for the downloaded export file.
         * Default 'export'.
         */
        exportFileName: 'export',

        /**
         * Show an "Export to PDF" button in the toolbar.
         * Renders all filtered/searched/sorted rows into a print-ready HTML page
         * and opens the browser print dialog (choose "Save as PDF").
         * Default true. Set false to hide it.
         */
        exportToPdf: true,

        /**
         * Additional CSS class(es) appended to the Export to PDF button.
         * The button already has `btn btn-sm btn-outline-danger`.
         * @example exportToPdfClass: 'ms-1'
         */
        exportToPdfClass: '',

        /**
         * Enable grouping by dragging column headers into the group bar.
         * When true, a group bar is rendered above the toolbar; drag any column
         * header label into it to group rows by that column's values.
         * Default false.
         */
        groupable: false,

        /**
         * Initial group fields array.
         * @example defaultGroups: ['Status', 'Region']
         */
        defaultGroups: [],

        /** Fires when the group state changes. ({ groups: ['field', …] }) */
        onGroupChange: null,

        /**
         * Allow users to remove groups by clicking × on group chips.
         * When false the × is hidden; the group state can only be changed
         * programmatically via setGroups() / clearGroups(). Default true.
         */
        groupRemovable: true,

        /**
         * Enable column reordering by dragging column header labels over each other.
         * When true, column labels become drag sources AND drop targets.
         * Compatible with groupable — the same drag can target either zone.
         * Default false.
         */
        reorderable: false,

        /** Fires when columns are reordered. ({ field, targetField }) */
        onColumnReorder: null
    };

    // ════════════════════════════════════════════════════════════════════════
    //  AddGrid constructor
    // ════════════════════════════════════════════════════════════════════════
    function AddGrid($el, options) {
        this.$el           = $el;
        this._el           = $el[0];
        this._opts         = $.extend({}, defaults, options);
        // Accept the originally requested misspelling as an alias, while exposing
        // the conventional `pageable` name as the supported public option.
        if (options && options.pabeble !== undefined && options.pageable === undefined) {
            this._opts.pageable = options.pabeble;
        }
        this._raw          = [];
        this._page         = 1;
        this._pageSize     = this._opts.pageSize || 50;
        this._sortState    = (this._opts.defaultSort || []).slice();
        this._filterState  = {};
        this._rowMap       = new Map();
        this._headerRow    = null;
        this._tbody        = null;
        this._footer       = null;
        this._filterDropdownEl = null;
        this._openFilter   = null;
        this._chipsBar     = null;
        this._selectedRows = new Set();
        this._htmlFields   = {};
        this._searchTerm   = String(this._opts.searchTerm || '').trim();
        this._searchInput  = null;
        this._searchClearBtn = null;
        this._searchChangePending = false;
        this._templateCache       = {};
        this._groupState          = (this._opts.defaultGroups || []).slice();
        this._collapsedGroups     = new Set();
        this._groupBar            = null;
        this._table               = null;
        this._scroll              = null;
        this._resizeState         = null;
        this._autoZoomPopupEl     = null;
        this._autoZoomCell        = null;

        const self = this;
        (this._opts.columns || []).forEach(function (c) {
            if (c.encoded === false || c.template) self._htmlFields[c.field] = true;
        });

        this._outsideClickHandler = function (e) {
            if (self._filterDropdownEl
                && !self._filterDropdownEl.contains(e.target)
                && !e.target.closest('.pg-filter-btn')) {
                self._closeFilter();
            }
        };

        if (this._opts.data) {
            this._raw = Array.isArray(this._opts.data) ? this._opts.data : [];
            this._renderAll();
        } else if (this._opts.url) {
            this._load();
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Prototype
    // ════════════════════════════════════════════════════════════════════════
    AddGrid.prototype = {

        // ── Event emission ───────────────────────────────────────────────────

        /**
         * Fire a named event both as a jQuery event and as an option callback.
         * @param {string} name  camelCase event name, e.g. 'rowClick'
         * @param {object} detail  Payload passed to handlers
         */
        _trigger: function (name, detail) {
            const cbKey = 'on' + name.charAt(0).toUpperCase() + name.slice(1);
            const data  = detail || {};

            this.$el.trigger('addgrid:' + name.toLowerCase(), [data]);

            if (typeof this._opts[cbKey] === 'function') {
                this._opts[cbKey].call(this._el, data);
            }
        },

        // ── Public API ────────────────────────────────────────────────────────

        /**
         * Reload data from the server (url mode) or re-render (static mode).
         */
        refresh: function () {
            if (this._opts.url) {
                this._load();
            } else {
                this._page = 1;
                if (this._tbody) { this._renderBody(); } else { this._renderAll(); }
            }
        },

        /**
         * Replace the grid's data with a new array (static / push mode).
         * Resets page, sort, and filter state.
         * @param {Array} arr  New data array
         */
        setData: function (arr, updateOptions) {
            const preserveState = updateOptions && updateOptions.preserveState === true;
            this._raw         = Array.isArray(arr) ? arr : [];
            if (!preserveState) {
                this._page        = 1;
                this._filterState = {};
                this._sortState   = (this._opts.defaultSort || []).slice();
            }
            this._selectedRows.clear();
            if (this._tbody) { this._renderBody(); } else { this._renderAll(); }
            this._trigger('dataBound', { data: this._raw, total: this._raw.length });
        },

        /**
         * Return the data item bound to a <tr> element.
         * @param {HTMLElement} trEl
         * @returns {object|null}
         */
        getDataItem: function (trEl) {
            return this._rowMap.get(trEl) || null;
        },

        /** Return a shallow copy of all source rows currently owned by the grid. */
        getData: function () {
            return this._raw.slice();
        },

        /** Replace the quick-search value and render the matching rows. */
        setSearch: function (term) {
            const nextTerm = String(term || '').trim();
            if (nextTerm === this._searchTerm) return;
            this._searchTerm = nextTerm;
            this._searchChangePending = true;
            this._page = 1;
            if (this._searchInput) this._searchInput.value = this._searchTerm;
            if (this._tbody) this._renderBody();
        },

        /** Return the current quick-search value. */
        getSearch: function () {
            return this._searchTerm;
        },

        /**
         * Return an array of data items for all currently selected rows.
         * @returns {Array}
         */
        getSelectedItems: function () {
            const self = this, result = [];
            self._selectedRows.forEach(function (tr) {
                const item = self._rowMap.get(tr);
                if (item) result.push(item);
            });
            return result;
        },

        /**
         * Clear the visual row selection without firing rowDeselect per row.
         */
        clearSelection: function () {
            this._selectedRows.forEach(function (tr) { tr.classList.remove('pg-row-selected'); });
            this._selectedRows.clear();
            this._trigger('selectionChange', { selected: [] });
        },

        /**
         * Remove all active column filters and re-render.
         */
        clearFilters: function () {
            this._filterState = {};
            this._page        = 1;
            this._renderBody();
            this._updateFilterBtns();
            this._trigger('filterChange', { filters: {} });
        },

        /**
         * Remove the active sort and re-render.
         */
        clearSort: function () {
            this._sortState = [];
            this._page      = 1;
            this._renderBody();
            this._updateSortIcons();
            this._trigger('sortChange', { sort: [] });
        },

        /**
         * Return a copy of the current sort state.
         * @returns {Array}  e.g. [{ field: 'Name', dir: 'asc' }]
         */
        getSortState: function () {
            return this._sortState.slice();
        },

        /**
         * Return a plain-object representation of the current filter state.
         * @returns {object}  e.g. { Status: ['Active', 'Pending'] }
         */
        getFilterState: function () {
            return this._serializeFilters();
        },

        /**
         * Destroy the grid instance: clear DOM content and remove plugin data.
         */
        destroy: function () {
            this._closeFilter();
            this._stopColumnResize();
            this._closeAutoZoom();
            this._rowMap.clear();
            this._selectedRows.clear();
            this._el.innerHTML = '';
            this.$el.removeData(DATA_KEY);
        },

        /**
         * Return a copy of the current group-by field list.
         * @returns {Array} e.g. ['Status', 'Region']
         */
        getGroupState: function () {
            return this._groupState.slice();
        },

        /**
         * Programmatically set the group fields and re-render.
         * Clears collapse state and resets to page 1.
         * @param {Array} fields  e.g. ['Status', 'Region']
         */
        setGroups: function (fields) {
            this._groupState = Array.isArray(fields) ? fields.slice() : [];
            this._collapsedGroups.clear();
            this._page = 1;
            this._renderAll();
            this._trigger('groupChange', { groups: this._groupState.slice() });
        },

        /**
         * Remove all active groups and re-render.
         */
        clearGroups: function () {
            this.setGroups([]);
        },

        // ── Data loading ──────────────────────────────────────────────────────

        _load: function () {
            const self = this;
            const visibleCount = self._opts.columns.filter(function (c) { return !c.hidden; }).length;

            self._trigger('beforeLoad');

            if (self._tbody) {
                self._tbody.innerHTML = '<tr><td colspan="' + visibleCount + '" class="pg-state-msg pg-loading" role="status" aria-live="polite">'
                    + '<span class="pg-loading-spinner pg-loading-sm"><span class="spinner-border spinner-border-sm" aria-hidden="true"></span></span>'
                    + '<span class="pg-loading-copy">Loading<span class="pg-loading-dots" aria-hidden="true"><span></span><span></span><span></span></span></span>'
                    + '</td></tr>';
            } else {
                self._el.innerHTML = '<div class="pg-state-msg pg-loading" role="status" aria-live="polite">'
                    + '<span class="pg-loading-spinner"><span class="spinner-border" aria-hidden="true"></span></span>'
                    + '<span class="pg-loading-copy">Loading<span class="pg-loading-dots" aria-hidden="true"><span></span><span></span><span></span></span></span>'
                    + '</div>';
            }

            $.ajax({
                url:      self._opts.url,
                type:     'GET',
                dataType: 'json',
                success: function (resp) {
                    const data = (self._opts.schema && self._opts.schema.data)
                        ? self._opts.schema.data(resp) : resp;
                    self._raw  = Array.isArray(data) ? data : [];
                    self._page = 1;
                    if (self._tbody) { self._renderBody(); } else { self._renderAll(); }
                    self._trigger('dataBound', { data: self._raw, total: self._raw.length });
                },
                error: function () {
                    const n = self._opts.columns.filter(function (c) { return !c.hidden; }).length;
                    const msg = '<tr><td colspan="' + n + '" class="pg-state-msg text-danger">'
                        + '<i class="bi bi-exclamation-circle me-1"></i>Failed to load data.</td></tr>';
                    if (self._tbody) { self._tbody.innerHTML = msg; }
                }
            });
        },

        // ── Text extraction (strips HTML for sort/filter) ─────────────────────

        _textOf: function (field, val) {
            if (val == null) return '';
            if (this._htmlFields[field] && String(val).indexOf('<') !== -1) {
                const d = document.createElement('div');
                d.innerHTML = val;
                return d.textContent || d.innerText || '';
            }
            return String(val);
        },

        _getColumn: function (field) {
            for (let i = 0; i < this._opts.columns.length; i++) {
                if (this._opts.columns[i].field === field) return this._opts.columns[i];
            }
            return null;
        },

        _columnValue: function (column, row, purpose) {
            if (!column) return '';
            const resolver = column[purpose + 'Value'];
            if (typeof resolver === 'function') return resolver.call(this._el, row, column);
            const field = column[purpose + 'Field'] || column.field;
            return row[field];
        },

        // ── Sort ─────────────────────────────────────────────────────────────

        _getSortDir: function (field) {
            for (let i = 0; i < this._sortState.length; i++) {
                if (this._sortState[i].field === field) return this._sortState[i].dir;
            }
            return null;
        },

        _toggleSort: function (field) {
            const dir = this._getSortDir(field);
            this._sortState = (dir === null)  ? [{ field: field, dir: 'asc' }]
                            : (dir === 'asc') ? [{ field: field, dir: 'desc' }]
                            : [];
            this._page = 1;
            this._renderBody();
            this._updateSortIcons();
            this._trigger('sortChange', { sort: this._sortState.slice() });
        },

        _applySort: function (data, sortOverride) {
            const self = this, s = sortOverride !== undefined ? sortOverride : this._sortState;
            if (!s.length) return data;
            return data.slice().sort(function (a, b) {
                for (let i = 0; i < s.length; i++) {
                    const f   = s[i].field;
                    const col = self._getColumn(f);
                    const aRaw = self._columnValue(col, a, 'sort');
                    const bRaw = self._columnValue(col, b, 'sort');
                    const av  = self._textOf(f, aRaw);
                    const bv  = self._textOf(f, bRaw);
                    const cmp = (typeof aRaw === 'number' && typeof bRaw === 'number')
                        ? aRaw - bRaw
                        : av.localeCompare(bv, undefined, { numeric: true, sensitivity: 'base' });
                    if (cmp !== 0) return s[i].dir === 'desc' ? -cmp : cmp;
                }
                return 0;
            });
        },

        _updateSortIcons: function () {
            const self = this;
            if (!self._headerRow) return;
            self._headerRow.querySelectorAll('.pg-sort-icon').forEach(function (el) {
                const dir  = self._getSortDir(el.dataset.field);
                const icon = el.querySelector('i');
                icon.className = dir === 'asc'  ? 'bi bi-arrow-up pg-sort-on'
                               : dir === 'desc' ? 'bi bi-arrow-down pg-sort-on'
                               :                  'bi bi-arrow-down-up';
            });
        },

        // ── Filter ────────────────────────────────────────────────────────────

        _applyFilter: function (data) {
            const self = this, fs = this._filterState, fields = Object.keys(fs);
            if (!fields.length) return data;
            return data.filter(function (row) {
                return fields.every(function (field) {
                    const s = fs[field];
                    const col = self._getColumn(field);
                    return !s || !s.size || s.has(self._textOf(field, self._columnValue(col, row, 'filter')));
                });
            });
        },

        _openFilterDropdown: function (field, btn) {
            const self = this;
            if (self._openFilter === field) { self._closeFilter(); return; }
            self._closeFilter();
            self._openFilter = field;

            const valMap = {};
            const column = self._getColumn(field);
            self._raw.forEach(function (r) {
                valMap[self._textOf(field, self._columnValue(column, r, 'filter'))] = true;
            });
            const vals = Object.keys(valMap).sort(function (a, b) {
                return a.localeCompare(b, undefined, { numeric: true });
            });
            const allowed = self._filterState[field] || null;

            const div = document.createElement('div');
            div.className = 'pg-filter-dropdown'
                + (self._opts.filterDropdownClass ? ' ' + self._opts.filterDropdownClass : '');
            div.innerHTML =
                '<div class="pg-filter-search"><input type="text" class="form-control form-control-sm" placeholder="Search…" /></div>'
                + '<div class="pg-filter-list">'
                + vals.map(function (v) {
                    const chk   = (!allowed || allowed.has(v)) ? ' checked' : '';
                    const label = v ? v.replace(/</g, '&lt;') : '<em class="text-muted">blank</em>';
                    return '<label class="pg-filter-item"><input type="checkbox" value="'
                        + v.replace(/"/g, '&quot;').replace(/</g, '&lt;') + '"' + chk
                        + '><span>' + label + '</span></label>';
                }).join('')
                + '</div>'
                + '<div class="pg-filter-footer">'
                + '<button class="btn btn-link btn-sm p-0 pg-filter-select-all">Toggle all</button>'
                + '<div class="d-flex gap-2">'
                + '<button class="btn btn-link btn-sm p-0 text-secondary pg-filter-close">Close</button>'
                + '<button class="btn btn-secondary btn-sm pg-filter-apply py-1 px-2 shadow">Apply</button>'
                + '</div>'
                + '</div>';

            const rect = btn.getBoundingClientRect();
            div.style.cssText = 'position:fixed;z-index:9999;top:' + (rect.bottom + 4) + 'px;left:' + rect.left + 'px';
            document.body.appendChild(div);

            // Clamp to viewport right edge
            const dr = div.getBoundingClientRect();
            if (dr.right > window.innerWidth - 8) {
                div.style.left = (window.innerWidth - dr.width - 8) + 'px';
            }
            self._filterDropdownEl = div;

            const applyBtn = div.querySelector('.pg-filter-apply');

            function syncApplyBtn() {
                applyBtn.disabled = !div.querySelectorAll('input[type=checkbox]:checked').length;
            }
            syncApplyBtn();

            div.querySelector('.form-control').addEventListener('input', function () {
                const q = this.value.toLowerCase();
                div.querySelectorAll('.pg-filter-item').forEach(function (item) {
                    item.style.display = item.querySelector('span').textContent.toLowerCase().includes(q) ? '' : 'none';
                });
            });
            div.querySelector('.pg-filter-select-all').addEventListener('click', function () {
                div.querySelectorAll('input[type=checkbox]').forEach(function (cb) { cb.checked = !cb.checked; });
                syncApplyBtn();
            });
            div.querySelectorAll('input[type=checkbox]').forEach(function (cb) {
                cb.addEventListener('change', syncApplyBtn);
            });
            div.querySelector('.pg-filter-close').addEventListener('click', function () {
                self._closeFilter();
            });
            applyBtn.addEventListener('click', function () {
                const checked = new Set();
                div.querySelectorAll('input[type=checkbox]:checked').forEach(function (cb) { checked.add(cb.value); });
                if (checked.size === vals.length) { delete self._filterState[field]; }
                else { self._filterState[field] = checked; }
                self._page = 1;
                self._closeFilter();
                self._renderBody();
                self._updateFilterBtns();
                self._trigger('filterChange', { filters: self._serializeFilters() });
            });
            div.addEventListener('click', function (e) { e.stopPropagation(); });
            setTimeout(function () { document.addEventListener('click', self._outsideClickHandler); }, 0);
        },

        _closeFilter: function () {
            document.removeEventListener('click', this._outsideClickHandler);
            if (this._filterDropdownEl) { this._filterDropdownEl.remove(); this._filterDropdownEl = null; }
            this._openFilter = null;
        },

        _updateFilterBtns: function () {
            const self = this;
            if (!self._headerRow) return;
            self._headerRow.querySelectorAll('.pg-filter-btn').forEach(function (btn) {
                const active = !!self._filterState[btn.dataset.field];
                btn.querySelector('i').className = active ? 'bi bi-funnel-fill' : 'bi bi-funnel';
                btn.classList.toggle('pg-filter-active', active);
            });
            self._updateChips();
        },

        _isColumnAutoZoomable: function (col) {
            return col && (col.autozoomable === true || (this._opts.autozoomable === true && col.autozoomable !== false));
        },

        _isCellEllipsized: function (tdEl) {
            if (!tdEl) return false;
            return tdEl.scrollWidth > tdEl.clientWidth || tdEl.scrollHeight > tdEl.clientHeight;
        },

        _closeAutoZoom: function () {
            if (this._autoZoomPopupEl) {
                this._autoZoomPopupEl.remove();
                this._autoZoomPopupEl = null;
            }
            this._autoZoomCell = null;
        },

        _openAutoZoom: function (tdEl, col) {
            if (!tdEl || !col || !this._isColumnAutoZoomable(col) || !this._isCellEllipsized(tdEl)) {
                this._closeAutoZoom();
                return;
            }

            if (this._autoZoomCell === tdEl && this._autoZoomPopupEl) return;

            this._closeAutoZoom();

            const popup = document.createElement('div');
            popup.className = 'pg-autozoom-popup';
            popup.textContent = tdEl.textContent || '';
            popup.style.visibility = 'hidden';
            document.body.appendChild(popup);

            const rect = tdEl.getBoundingClientRect();
            popup.style.minWidth = Math.max(MIN_RESIZABLE_COLUMN_WIDTH, Math.ceil(rect.width)) + 'px';

            const popupRect = popup.getBoundingClientRect();
            let top = rect.bottom + 4;
            let left = rect.left;

            if (left + popupRect.width > window.innerWidth - 8) {
                left = Math.max(8, window.innerWidth - popupRect.width - 8);
            }
            if (top + popupRect.height > window.innerHeight - 8) {
                top = rect.top - popupRect.height - 4;
            }
            if (top < 8) {
                top = Math.max(8, rect.bottom + 4);
            }

            popup.style.left = left + 'px';
            popup.style.top = top + 'px';
            popup.style.visibility = 'visible';

            this._autoZoomPopupEl = popup;
            this._autoZoomCell = tdEl;
        },

        _stopColumnResize: function () {
            if (!this._resizeState) return;
            document.removeEventListener('mousemove', this._resizeState.onMouseMove);
            document.removeEventListener('mouseup', this._resizeState.onMouseUp);
            document.body.classList.remove('pg-col-resizing');
            this._resizeState = null;
        },

        _captureColumnWidths: function () {
            if (!this._table || !this._headerRow) return null;

            const self = this;
            const visibleCols = self._opts.columns.filter(function (c) {
                if (c.hidden) return false;
                if (self._opts.groupRemovable !== false && self._groupState.indexOf(c.field) !== -1) return false;
                return true;
            });
            const colEls = this._table.querySelectorAll('colgroup col');
            const thEls = this._headerRow.children;
            const widths = [];

            Array.prototype.forEach.call(thEls, function (thEl, idx) {
                const width = Math.max(MIN_RESIZABLE_COLUMN_WIDTH, thEl.getBoundingClientRect().width);
                widths.push(width);
            });

            return {
                visibleCols: visibleCols,
                colEls: colEls,
                thEls: thEls,
                widths: widths,
                tableWidth: Math.max(MIN_RESIZABLE_COLUMN_WIDTH, this._table.getBoundingClientRect().width)
            };
        },

        _freezeColumnWidths: function (snapshot) {
            if (!snapshot) return [];

            Array.prototype.forEach.call(snapshot.thEls, function (thEl, idx) {
                const width = snapshot.widths[idx];

                if (snapshot.visibleCols[idx]) snapshot.visibleCols[idx].width = width;
                if (snapshot.colEls[idx]) {
                    snapshot.colEls[idx].style.width = width + 'px';
                    snapshot.colEls[idx].style.minWidth = MIN_RESIZABLE_COLUMN_WIDTH + 'px';
                }
                thEl.style.width = width + 'px';
                thEl.style.minWidth = MIN_RESIZABLE_COLUMN_WIDTH + 'px';
            });

            this._table.style.minWidth = '0';
            this._table.style.tableLayout = 'fixed';
            this._table.style.width = snapshot.tableWidth + 'px';

            return snapshot.widths;
        },

        _measureHeaderWidth: function (thEl) {
            if (!thEl) return MIN_RESIZABLE_COLUMN_WIDTH;

            const host = document.createElement('div');
            host.style.cssText = 'position:absolute;visibility:hidden;left:-9999px;top:-9999px;z-index:-1';

            const inner = thEl.querySelector('.pg-th-inner');
            const clone = inner ? inner.cloneNode(true) : document.createElement('div');
            if (!inner) clone.textContent = thEl.textContent || '';
            clone.style.width = 'auto';
            clone.style.minWidth = '0';
            clone.style.maxWidth = 'none';

            host.appendChild(clone);
            document.body.appendChild(host);

            const styles = window.getComputedStyle(thEl);
            const width = clone.getBoundingClientRect().width
                + parseFloat(styles.paddingLeft || '0')
                + parseFloat(styles.paddingRight || '0');

            document.body.removeChild(host);
            return Math.ceil(Math.max(MIN_RESIZABLE_COLUMN_WIDTH, width));
        },

        _measureCellWidth: function (tdEl) {
            if (!tdEl) return MIN_RESIZABLE_COLUMN_WIDTH;

            const host = document.createElement('div');
            host.style.cssText = 'position:absolute;visibility:hidden;left:-9999px;top:-9999px;z-index:-1';

            const table = document.createElement('table');
            table.className = 'pg-grid-table';
            table.style.cssText = 'width:auto;min-width:0;max-width:none;table-layout:auto;border-collapse:collapse';

            const tbody = document.createElement('tbody');
            const tr = document.createElement('tr');
            const clone = tdEl.cloneNode(true);

            clone.style.width = 'auto';
            clone.style.minWidth = '0';
            clone.style.maxWidth = 'none';
            clone.style.overflow = 'visible';
            clone.style.textOverflow = 'clip';

            tr.appendChild(clone);
            tbody.appendChild(tr);
            table.appendChild(tbody);
            host.appendChild(table);
            document.body.appendChild(host);

            const width = clone.getBoundingClientRect().width;

            document.body.removeChild(host);
            return Math.ceil(Math.max(MIN_RESIZABLE_COLUMN_WIDTH, width));
        },

        _getAutoFitColumnWidth: function (thEl, columnIndex) {
            let maxWidth = this._measureHeaderWidth(thEl);
            if (!this._tbody) return maxWidth;

            Array.prototype.forEach.call(this._tbody.rows, function (row) {
                if (!row || !row.children || row.children.length <= columnIndex) return;
                if (row.children.length !== (this._headerRow ? this._headerRow.children.length : 0)) return;

                const cell = row.children[columnIndex];
                if (!cell || cell.colSpan > 1) return;

                maxWidth = Math.max(maxWidth, this._measureCellWidth(cell));
            }, this);

            return Math.max(MIN_RESIZABLE_COLUMN_WIDTH, maxWidth);
        },

        _autoFitColumn: function (col, thEl, colEl) {
            this._stopColumnResize();

            const snapshot = this._captureColumnWidths();
            if (!snapshot) return;

            const thEls = this._headerRow ? this._headerRow.children : [];
            const targetIdx = Array.prototype.indexOf.call(thEls, thEl);
            if (targetIdx === -1) return;

            this._freezeColumnWidths(snapshot);

            const nextWidth = this._getAutoFitColumnWidth(thEl, targetIdx);
            const widthDelta = nextWidth - snapshot.widths[targetIdx];

            col.width = nextWidth;
            if (colEl) {
                colEl.style.width = nextWidth + 'px';
                colEl.style.minWidth = MIN_RESIZABLE_COLUMN_WIDTH + 'px';
            }
            thEl.style.width = nextWidth + 'px';
            thEl.style.minWidth = MIN_RESIZABLE_COLUMN_WIDTH + 'px';
            this._table.style.width = Math.max(MIN_RESIZABLE_COLUMN_WIDTH, snapshot.tableWidth + widthDelta) + 'px';
        },

        _startColumnResize: function (e, col, thEl, colEl) {
            if (e.button !== 0) return;

            e.preventDefault();
            e.stopPropagation();

            this._stopColumnResize();

            const snapshot = this._captureColumnWidths();
            const thEls = this._headerRow ? this._headerRow.children : [];
            const targetIdx = Array.prototype.indexOf.call(thEls, thEl);
            const startWidth = snapshot && snapshot.widths[targetIdx]
                ? snapshot.widths[targetIdx]
                : Math.max(MIN_RESIZABLE_COLUMN_WIDTH, thEl.getBoundingClientRect().width);
            const totalWidth = snapshot ? snapshot.tableWidth : Math.max(MIN_RESIZABLE_COLUMN_WIDTH, this._table.getBoundingClientRect().width);
            const self = this;

            const state = {
                column: col,
                columnEl: colEl,
                headerEl: thEl,
                startX: e.clientX,
                startWidth: startWidth,
                totalWidth: totalWidth,
                snapshot: snapshot,
                hasMoved: false
            };

            state.onMouseMove = function (moveEvent) {
                if (!state.hasMoved) {
                    self._freezeColumnWidths(state.snapshot);
                    state.hasMoved = true;
                }

                const delta = moveEvent.clientX - state.startX;
                const nextWidth = Math.max(MIN_RESIZABLE_COLUMN_WIDTH, state.startWidth + delta);
                const widthDelta = nextWidth - state.startWidth;

                state.column.width = nextWidth;
                if (state.columnEl) state.columnEl.style.width = nextWidth + 'px';
                state.headerEl.style.width = nextWidth + 'px';
                state.headerEl.style.minWidth = MIN_RESIZABLE_COLUMN_WIDTH + 'px';
                self._table.style.width = Math.max(MIN_RESIZABLE_COLUMN_WIDTH, state.totalWidth + widthDelta) + 'px';
            };

            state.onMouseUp = function () {
                self._stopColumnResize();
            };

            this._resizeState = state;
            document.body.classList.add('pg-col-resizing');
            document.addEventListener('mousemove', state.onMouseMove);
            document.addEventListener('mouseup', state.onMouseUp);
        },

        _updateChips: function () {
            const self = this;
            if (!self._chipsBar) return;

            self._chipsBar.innerHTML = '';
            const fields = Object.keys(self._filterState);

            if (!fields.length) {
                self._chipsBar.style.display = 'none';
                return;
            }

            // Build a field → title lookup from column definitions
            const titleMap = {};
            self._opts.columns.forEach(function (c) { titleMap[c.field] = c.title || c.field; });

            fields.forEach(function (field) {
                const values  = Array.from(self._filterState[field]).filter(function (v) { return v !== ''; });
                if (!values.length) return;

                const label   = titleMap[field] || field;
                const summary = values.length > 2
                    ? values.slice(0, 2).join(', ') + ' +' + (values.length - 2)
                    : values.join(', ');

                const chip = document.createElement('span');
                chip.className = 'pg-chip';
                chip.innerHTML =
                    '<span class="pg-chip-label">' + label + '</span>'
                    + '<span class="pg-chip-arrow">➜</span>'
                    + '<span class="pg-chip-value">' + summary + '</span>'
                    + '<button type="button" class="pg-chip-remove" title="Remove filter"><i class="bi bi-x"></i></button>';

                chip.querySelector('.pg-chip-remove').addEventListener('click', function () {
                    delete self._filterState[field];
                    self._page = 1;
                    self._renderBody();
                    self._updateFilterBtns();
                    self._trigger('filterChange', { filters: self._serializeFilters() });
                });

                self._chipsBar.appendChild(chip);
            });

            // Only show "Clear filters" if at least one chip was rendered
            if (!self._chipsBar.children.length) {
                self._chipsBar.style.display = 'none';
                return;
            }

            // Clear all button
            const clearBtn = document.createElement('button');
            clearBtn.type = 'button';
            clearBtn.className = 'pg-chips-clear-all';
            clearBtn.innerHTML = '<i class="bi bi-x-circle me-1"></i>Clear filters';
            clearBtn.addEventListener('click', function () {
                self.clearFilters();
            });
            self._chipsBar.appendChild(clearBtn);

            self._chipsBar.style.display = '';
        },

        _serializeFilters: function () {
            const result = {}, self = this;
            Object.keys(self._filterState).forEach(function (field) {
                result[field] = Array.from(self._filterState[field]);
            });
            return result;
        },

        _resolveOptionValue: function (option, item) {
            return typeof option === 'function' ? option.call(this._el, item) : option;
        },

        _applyAttributes: function (element, attributes) {
            if (!element || !attributes) return;
            Object.keys(attributes).forEach(function (name) {
                const value = attributes[name];
                if (value === undefined || value === null || value === false) return;
                element.setAttribute(name, value === true ? '' : String(value));
            });
        },

        _applyDataRowPresentation: function (tr, item) {
            const rowClass = this._resolveOptionValue(this._opts.rowClass, item);
            if (rowClass) {
                String(rowClass).split(/\s+/).filter(Boolean).forEach(function (className) {
                    tr.classList.add(className);
                });
            }
            this._applyAttributes(tr, this._resolveOptionValue(this._opts.rowAttributes, item));
        },

        _appendDetailRow: function (item, cols, fragment) {
            if (!this._opts.detailTemplate) return;

            const detailRow = document.createElement('tr');
            const detailCell = document.createElement('td');
            const detailId = this._resolveOptionValue(this._opts.detailRowId, item);
            const detailClass = this._resolveOptionValue(this._opts.detailRowClass, item);
            const detailCellClass = this._resolveOptionValue(this._opts.detailCellClass, item);
            const expanded = this._opts.detailExpanded == null
                ? false
                : !!this._resolveOptionValue(this._opts.detailExpanded, item);

            if (detailId) detailRow.id = String(detailId);
            if (detailClass) detailRow.className = String(detailClass);
            if (!expanded) detailRow.classList.add('d-none');
            if (detailCellClass) detailCell.className = String(detailCellClass);
            detailCell.colSpan = cols.length;
            detailCell.innerHTML = this._compileTemplate(this._opts.detailTemplate, item);
            this._applyAttributes(detailRow, this._resolveOptionValue(this._opts.detailRowAttributes, item));
            detailRow.appendChild(detailCell);
            fragment.appendChild(detailRow);
        },

        // ── Render ────────────────────────────────────────────────────────────

        _renderAll: function () {
            const self = this;
            self._stopColumnResize();
            self._closeAutoZoom();
            const cols = self._opts.columns.filter(function (c) {
                if (c.hidden) return false;
                // When groupRemovable, grouped columns are hidden from the header (their value shows in the group row)
                if (self._opts.groupRemovable !== false && self._groupState.indexOf(c.field) !== -1) return false;
                return true;
            });

            self._el.innerHTML = '';
            const wrap   = document.createElement('div');   wrap.className   = 'pg-grid-wrapper';
            const scroll = document.createElement('div');   scroll.className = 'pg-grid-scroll';
            const table  = document.createElement('table'); table.className  = 'pg-grid-table';
            const thead  = document.createElement('thead');
            const hRow   = document.createElement('tr');
            const hasSizedColumns = cols.some(function (c) { return c.width != null; });

            if (self._opts.height) wrap.style.height = self._opts.height;
            if (self._opts.tableId) table.id = self._opts.tableId;
            if (self._opts.tableClass) table.className += ' ' + self._opts.tableClass;
            table.style.tableLayout = hasSizedColumns ? 'fixed' : 'auto';

            // ── colgroup — one <col> per visible column ─────────────────────
            const colgroup = document.createElement('colgroup');
            cols.forEach(function (col) {
                const c = document.createElement('col');
                if (col.width) {
                    c.style.width = typeof col.width === 'number' ? col.width + 'px' : col.width;
                }
                colgroup.appendChild(c);
            });
            table.appendChild(colgroup);

            cols.forEach(function (col, colIdx) {
                const th = document.createElement('th');
                if (col.width) {
                    th.style.width = typeof col.width === 'number' ? col.width + 'px' : col.width;
                    th.style.minWidth = typeof col.width === 'number' ? col.width + 'px' : col.width;
                }

                if (col.field === 'Selection') {
                    th.className = 'pg-col-cb';
                    th.innerHTML = '<input type="checkbox" class="pg-select-all-cb" title="Select all" />';
                } else {
                    const canSort   = self._opts.sortable !== false && col.sortable !== false;
                    const canFilter = self._opts.filterable !== false && col.filterable !== false;
                    th.className = 'pg-th'
                        + (canSort   ? ' pg-th-sortable'   : '')
                        + (canFilter ? ' pg-th-filterable' : '');

                    const filterHtml = canFilter
                        ? '<button type="button" class="pg-filter-btn" data-field="' + col.field
                            + '" title="Filter"><i class="bi bi-funnel"></i></button>'
                        : '';
                    const sortHtml = canSort
                        ? '<span class="pg-sort-icon" data-field="' + col.field
                            + '"><i class="bi bi-arrow-down-up"></i></span>'
                        : '';

                    const innerClass = !canFilter && !canSort
                        ? 'pg-th-inner pg-th-plain'
                        : !canFilter
                            ? 'pg-th-inner pg-th-sort-only'
                            : !canSort
                                ? 'pg-th-inner pg-th-filter-only'
                                : 'pg-th-inner';
                    th.innerHTML = '<div class="' + innerClass + '">'
                        + filterHtml
                        + '<span class="pg-th-label">' + (col.title != null ? col.title : col.field) + '</span>'
                        + sortHtml
                        + '</div>';

                    if (canSort) {
                        (function (f) {
                            const fn = function () { self._toggleSort(f); };
                            th.querySelector('.pg-th-label').addEventListener('click', fn);
                            th.querySelector('.pg-sort-icon').addEventListener('click', fn);
                        }(col.field));
                    }
                    if (canFilter) {
                        (function (f) {
                            th.querySelector('.pg-filter-btn').addEventListener('click', function (e) {
                                e.stopPropagation();
                                self._openFilterDropdown(f, this);
                            });
                        }(col.field));
                    }
                    // ── Drag source: used by both group-bar drop AND column reorder ──
                    if ((self._opts.groupable || self._opts.reorderable)
                        && col.field !== 'Selection'
                        && col.reorderable !== false) {
                        (function (field, label, thEl) {
                            const handle = thEl.querySelector('.pg-th-label');
                            if (!handle) return;
                            handle.draggable = true;
                            handle.classList.add('pg-th-draggable');
                            handle.addEventListener('dragstart', function (e) {
                                e.dataTransfer.setData('text/add-field', field);
                                e.dataTransfer.effectAllowed = 'all';
                                thEl.classList.add('pg-th-dragging');
                                // Styled ghost pill — overrides the default browser drag image
                                const ghost = document.createElement('div');
                                ghost.className = 'pg-drag-ghost';
                                ghost.innerHTML =
                                    '<i class="bi bi-layers"></i>'
                                    + '<span>' + self._escapeHtml(label) + '</span>'
                                    + '<i class="bi bi-grip-vertical pg-drag-ghost-grip"></i>';
                                ghost.style.cssText = 'position:fixed;top:-9999px;left:-9999px;z-index:99999';
                                document.body.appendChild(ghost);
                                self._dragGhost = ghost;
                                e.dataTransfer.setDragImage(
                                    ghost,
                                    Math.round(ghost.offsetWidth  / 2),
                                    Math.round(ghost.offsetHeight / 2)
                                );
                            });
                            handle.addEventListener('dragend', function () {
                                thEl.classList.remove('pg-th-dragging');
                                // Clear any lingering drop indicators (e.g. if drag was cancelled)
                                if (self._headerRow) {
                                    self._headerRow.querySelectorAll('.pg-th-drop-before,.pg-th-drop-after')
                                        .forEach(function (el) {
                                            el.classList.remove('pg-th-drop-before', 'pg-th-drop-after');
                                        });
                                }
                                if (self._dragGhost) { self._dragGhost.remove(); self._dragGhost = null; }
                            });
                        }(col.field, col.title || col.field, th));
                    }

                    // ── Drop target: column reorder ───────────────────────────
                    if (self._opts.reorderable && col.field !== 'Selection' && col.reorderable !== false) {
                        (function (field, thEl) {
                            thEl.addEventListener('dragover', function (e) {
                                const types = e.dataTransfer.types;
                                // Only accept column-header drags; ignore group-chip reorder
                                if ([].indexOf.call(types, 'text/add-field') === -1) return;
                                if ([].indexOf.call(types, 'text/add-group-idx') !== -1) return;
                                e.preventDefault();
                                e.dataTransfer.dropEffect = 'move';
                                const rect = thEl.getBoundingClientRect();
                                const before = e.clientX < rect.left + rect.width / 2;
                                thEl.classList.toggle('pg-th-drop-before',  before);
                                thEl.classList.toggle('pg-th-drop-after',  !before);
                            });
                            thEl.addEventListener('dragleave', function (e) {
                                if (!thEl.contains(e.relatedTarget)) {
                                    thEl.classList.remove('pg-th-drop-before', 'pg-th-drop-after');
                                }
                            });
                            thEl.addEventListener('drop', function (e) {
                                e.preventDefault();
                                thEl.classList.remove('pg-th-drop-before', 'pg-th-drop-after');
                                const fromField = e.dataTransfer.getData('text/add-field');
                                if (!fromField || fromField === field) return;
                                const allCols = self._opts.columns;
                                let fromIdx = -1;
                                allCols.forEach(function (c, i) { if (c.field === fromField) fromIdx = i; });
                                if (fromIdx === -1 || allCols[fromIdx].reorderable === false) return;
                                const rect = thEl.getBoundingClientRect();
                                const insertBefore = e.clientX < rect.left + rect.width / 2;
                                const dragged = allCols.splice(fromIdx, 1)[0];
                                // Recompute target index after removal
                                let toIdx = -1;
                                allCols.forEach(function (c, i) { if (c.field === field) toIdx = i; });
                                if (toIdx === -1)     { allCols.push(dragged); }
                                else if (insertBefore){ allCols.splice(toIdx, 0, dragged); }
                                else                  { allCols.splice(toIdx + 1, 0, dragged); }
                                self._renderAll();
                                self._trigger('columnReorder', { field: fromField, targetField: field });
                            });
                        }(col.field, th));
                    }
                }

                if (self._opts.resizable === true && col.field !== 'Selection' && col.resizable !== false) {
                    (function (column, thEl, colEl) {
                        const resizeHandle = document.createElement('button');
                        resizeHandle.type = 'button';
                        resizeHandle.className = 'pg-resize-handle';
                        resizeHandle.setAttribute('aria-label', 'Resize ' + (column.title || column.field) + ' column');
                        resizeHandle.title = 'Resize column';
                        resizeHandle.addEventListener('mousedown', function (evt) {
                            self._startColumnResize(evt, column, thEl, colEl);
                        });
                        resizeHandle.addEventListener('dblclick', function (evt) {
                            evt.preventDefault();
                            evt.stopPropagation();
                            self._autoFitColumn(column, thEl, colEl);
                        });
                        thEl.appendChild(resizeHandle);
                    }(col, th, colgroup.children[colIdx]));
                }

                const headerClass = typeof col.headerClass === 'function'
                    ? col.headerClass.call(self._el, col)
                    : col.headerClass;
                if (headerClass) th.className += ' ' + headerClass;

                hRow.appendChild(th);
            });

            const tbody  = document.createElement('tbody');
            if (self._opts.tbodyId) tbody.id = self._opts.tbodyId;
            if (self._opts.tbodyClass) tbody.className = self._opts.tbodyClass;
            const footer = self._opts.pageable !== false || self._opts.showRecordCount === true
                ? document.createElement('div')
                : null;
            if (footer) {
                footer.className = 'pg-grid-footer';
                footer.setAttribute('aria-live', 'polite');
            }

            thead.appendChild(hRow);
            table.appendChild(thead);
            table.appendChild(tbody);
            scroll.appendChild(table);

            // ── Toolbar: search bar + export buttons ───────────────────────
            if ((self._opts.searchable !== false && self._opts.showSearch !== false) || self._opts.exportToExcel !== false || self._opts.exportToPdf !== false) {
                const searchBar = self._buildSearchBar();
                // ── Inline group bar (between export buttons and search input) ──
                if (self._opts.groupable) {
                    const groupBarEl = self._buildGroupBar();
                    const searchInputGroup = searchBar.querySelector('.pg-search-input-group');
                    searchBar.insertBefore(groupBarEl, searchInputGroup || null);
                }
                wrap.appendChild(searchBar);
                // Restore typed search term when _renderAll is called due to group changes
                if (self._searchInput && self._searchTerm) {
                    self._searchInput.value = self._searchTerm;
                }
            } else if (self._opts.groupable) {
                // No toolbar at all — standalone group bar
                wrap.appendChild(self._buildGroupBar());
            }

            // ── Filter chips bar ────────────────────────────────────────────
            if (self._opts.showFilterChips !== false) {
                const chipsBar = document.createElement('div');
                chipsBar.className = 'pg-filter-chips-bar';
                chipsBar.style.display = 'none';
                wrap.appendChild(chipsBar);
                self._chipsBar = chipsBar;
            }

            wrap.appendChild(scroll);
            if (footer) wrap.appendChild(footer);
            self._el.appendChild(wrap);

            self._headerRow = hRow;
            self._tbody     = tbody;
            self._footer    = footer;
            self._table     = table;
            self._scroll    = scroll;

            if (self._scroll) {
                self._scroll.addEventListener('scroll', function () {
                    self._closeAutoZoom();
                });
            }

            self._renderBody();
            self._updateSortIcons();
        },

        _renderBody: function () {
            const self = this;
            self._closeAutoZoom();
            const cols = self._opts.columns.filter(function (c) {
                if (c.hidden) return false;
                if (self._opts.groupRemovable !== false && self._groupState.indexOf(c.field) !== -1) return false;
                return true;
            });

            const filtered  = self._applyFilter(self._raw);
            const searched  = self._applySearch(filtered);
            self._rowMap.clear();
            self._selectedRows.clear();

            const frag = document.createDocumentFragment();

            if (self._groupState.length) {
                // Grouped mode: sort by group fields first, then user sort; render all rows
                const gSorted = self._applySort(searched, self._getEffectiveSort());

                if (!gSorted.length) {
                    const getr = document.createElement('tr');
                    const getd = document.createElement('td');
                    getd.colSpan  = cols.length;
                    getd.className = 'pg-state-msg pg-empty';
                    getd.innerHTML = emptyStateMarkup(self._opts.emptyText, self._opts.emptyHint);
                    getr.appendChild(getd);
                    frag.appendChild(getr);
                } else {
                    self._renderGroupRows(self._applyGroup(gSorted), cols, frag, 0);
                }

                self._tbody.innerHTML = '';
                self._tbody.appendChild(frag);

                if (self._searchInput && self._searchChangePending) {
                    self._trigger('searchChange', { term: self._searchTerm, matchCount: gSorted.length });
                    self._searchChangePending = false;
                }

                // Grouped footer: show row count with a "grouped" badge, no page nav.
                if (self._footer) {
                    self._footer.innerHTML = '<div class="pg-pager">'
                        + self._recordInfoHtml(
                            gSorted.length,
                            null,
                            null,
                            '<span class="pg-pager-info-page"><i class="bi bi-collection me-1"></i>grouped</span>'
                        )
                        + '</div>';
                }
            } else {
                const sorted    = self._applySort(searched);
                const total    = sorted.length;
                const pageable = self._opts.pageable !== false;
                const start    = pageable ? (self._page - 1) * self._pageSize : 0;
                const end      = pageable ? Math.min(start + self._pageSize, total) : total;
                const pageData = pageable ? sorted.slice(start, end) : sorted;

                if (!pageData.length) {
                    const etr = document.createElement('tr');
                    const etd = document.createElement('td');
                    etd.colSpan  = cols.length;
                    etd.className = 'pg-state-msg pg-empty';
                    etd.innerHTML = emptyStateMarkup(self._opts.emptyText, self._opts.emptyHint);
                    etr.appendChild(etd);
                    frag.appendChild(etr);
                } else {
                    pageData.forEach(function (item, idx) {
                        const tr = document.createElement('tr');
                        tr.className = (self._opts.alternateRows && idx % 2 !== 0) ? 'pg-row-odd' : 'pg-row-even';
                        self._applyDataRowPresentation(tr, item);

                        tr.addEventListener('click', function (e) {
                            self._handleRowClick(tr, item, e);
                        });
                        tr.addEventListener('dblclick', function (e) {
                            if (self._handleCopyableDblClick(e)) return;
                            self._trigger('rowDblClick', { dataItem: item, rowElement: tr, event: e });
                        });
                        tr.addEventListener('contextmenu', function (e) {
                            self._trigger('rowContextMenu', { dataItem: item, rowElement: tr, event: e });
                        });

                        cols.forEach(function (col, colIdx) {
                            const td  = document.createElement('td');
                            if (col.field === 'Selection') td.className = 'pg-col-cb';
                            let val = item[col.field];
                            if (val == null) val = '';

                            const term         = self._searchTerm;
                            const canHighlight = term && col.searchable !== false
                                && col.encoded !== false
                                && !col.template
                                && col.field !== 'Selection';

                            if (col.template) {
                                td.innerHTML = self._compileTemplate(col.template, item);
                                if (term && col.searchable !== false) self._highlightTemplateCell(td, term);
                            } else if (col.encoded === false) {
                                td.innerHTML = String(val);
                                if (term && col.searchable !== false) self._highlightTemplateCell(td, term);
                            } else if (canHighlight) {
                                td.innerHTML = self._highlightMatch(String(val), term);
                            } else {
                                td.textContent = String(val);
                            }

                            const cellClass = typeof col.cellClass === 'function'
                                ? col.cellClass.call(self._el, item, val, col)
                                : col.cellClass;
                            if (cellClass) td.className += (td.className ? ' ' : '') + cellClass;

                            td.addEventListener('click', function (e) {
                                self._trigger('cellClick', {
                                    dataItem: item, field: col.field, value: val,
                                    cellElement: td, rowElement: tr, columnIndex: colIdx, event: e
                                });
                            });
                            td.addEventListener('dblclick', function (e) {
                                if (self._handleCopyableDblClick(e)) return;
                                e.stopPropagation(); // prevent rowDblClick from also firing
                                self._trigger('cellDblClick', {
                                    dataItem: item, field: col.field, value: val,
                                    cellElement: td, rowElement: tr, columnIndex: colIdx, event: e
                                });
                            });
                            if (self._isColumnAutoZoomable(col)) {
                                td.addEventListener('mouseenter', function () {
                                    self._openAutoZoom(td, col);
                                });
                                td.addEventListener('mouseleave', function () {
                                    self._closeAutoZoom();
                                });
                            }

                            tr.appendChild(td);
                        });

                        self._rowMap.set(tr, item);
                        frag.appendChild(tr);
                        self._appendDetailRow(item, cols, frag);
                    });
                }

                self._tbody.innerHTML = '';
                self._tbody.appendChild(frag);

                // Update match count badge in search bar
                if (self._searchInput && self._searchChangePending) {
                    self._trigger('searchChange', { term: self._searchTerm, matchCount: total });
                    self._searchChangePending = false;
                }

                if (pageable) {
                    self._renderPager(total, start + 1, Math.min(end, total));
                } else if (self._opts.showRecordCount === true) {
                    self._renderRecordCount(total, searched);
                }
            }
            self._trigger('render', { data: searched, total: searched.length });
        },

        // ── Search ────────────────────────────────────────────────────────────

        _buildSearchBar: function () {
            const self = this;
            const bar  = document.createElement('div');
            bar.className = 'pg-search-bar';

            // ── Export buttons group (left side) ──────────────────────────
            const hasExcel = self._opts.exportToExcel !== false;
            const hasPdf   = self._opts.exportToPdf   !== false;

            if (hasExcel || hasPdf) {
                const exportGroup       = document.createElement('div');
                exportGroup.className = 'pg-export-group';

                if (hasExcel) {
                    let excelClass = 'btn btn-sm btn-outline-success pg-export-excel-btn';
                    if (self._opts.exportToExcelClass) excelClass += ' ' + self._opts.exportToExcelClass;
                    const excelBtn       = document.createElement('button');
                    excelBtn.type      = 'button';
                    excelBtn.className = excelClass;
                    excelBtn.innerHTML = '<i class="bi bi-file-earmark-excel me-1"></i>Excel';
                    excelBtn.addEventListener('click', function () { self._exportToExcel(); });
                    exportGroup.appendChild(excelBtn);
                }

                if (hasPdf) {
                    let pdfClass = 'btn btn-sm btn-outline-danger pg-export-pdf-btn shadow';
                    if (self._opts.exportToPdfClass) pdfClass += ' ' + self._opts.exportToPdfClass;
                    const pdfBtn       = document.createElement('button');
                    pdfBtn.type      = 'button';
                    pdfBtn.className = pdfClass;
                    pdfBtn.innerHTML = '<i class="bi bi-file-earmark-pdf me-1"></i>PDF';
                    pdfBtn.addEventListener('click', function () { self._exportToPdf(); });
                    exportGroup.appendChild(pdfBtn);
                }

                bar.appendChild(exportGroup);
            }

            // ── Search input group (right side) ────────────────────────────
            if (self._opts.searchable !== false && self._opts.showSearch !== false) {
                const searchGroup       = document.createElement('div');
                searchGroup.className = 'input-group input-group-sm pg-search-input-group shadow';
                searchGroup.innerHTML =
                    '<span class="input-group-text"><i class="bi bi-search"></i></span>'
                    + '<input type="text" class="form-control pg-search-input" placeholder="Search…" />'
                    + '<button type="button" class="btn btn-light pg-search-clear" title="Clear">'
                    + '<i class="bi bi-x-lg"></i></button>';
                bar.appendChild(searchGroup);

                self._searchInput    = searchGroup.querySelector('.pg-search-input');
                self._searchClearBtn = searchGroup.querySelector('.pg-search-clear');
                self._searchInput.placeholder = self._opts.searchPlaceholder || 'Search…';
                self._searchInput.setAttribute('aria-label', self._searchInput.placeholder);
                self._searchInput.value = self._searchTerm;

                const doSearch = debounce(function () {
                    const nextTerm = self._searchInput.value.trim();
                    if (nextTerm === self._searchTerm) return;
                    self._searchTerm = nextTerm;
                    self._searchChangePending = true;
                    self._page = 1;
                    self._renderBody();
                }, 200);

                self._searchInput.addEventListener('input', doSearch);

                self._searchClearBtn.addEventListener('click', function () {
                    self._searchInput.value = '';
                    if (!self._searchTerm) {
                        self._searchInput.focus();
                        return;
                    }
                    self._searchTerm = '';
                    self._searchChangePending = true;
                    self._page = 1;
                    self._renderBody();
                    self._searchInput.focus();
                });
            }

            return bar;
        },

        _applySearch: function (data) {
            const self  = this;
            const terms = (self._searchTerm || '').toLowerCase().split(' ').filter(function (t) { return t.length > 0; });
            if (!terms.length) return data;
            if (typeof self._opts.searchPredicate === 'function') {
                return data.filter(function (row) {
                    return self._opts.searchPredicate.call(self._el, row, self._searchTerm);
                });
            }
            const searchCols = self._opts.columns.filter(function (c) {
                return !c.hidden && c.searchable !== false && c.field !== 'Selection';
            });
            if (!searchCols.length) return data;
            return data.filter(function (row) {
                return terms.every(function (term) {
                    return searchCols.some(function (col) {
                        if (col.searchValue || col.searchField) {
                            return self._textOf(col.field, self._columnValue(col, row, 'search')).toLowerCase().indexOf(term) >= 0;
                        }
                        if (col.template) {
                            const d = document.createElement('div');
                            d.innerHTML = self._compileTemplate(col.template, row);
                            return (d.textContent || d.innerText || '').toLowerCase().indexOf(term) >= 0;
                        }
                        return self._textOf(col.field, row[col.field]).toLowerCase().indexOf(term) >= 0;
                    });
                });
            });
        },

        _highlightMatch: function (text, query) {
            const source  = (text == null ? '' : String(text));
            const value   = (query || '').trim();
            if (!source) return '';
            if (!value)  return this._escapeHtml(source);
            const tokens  = value.split(' ').filter(function (t) { return t.length > 0; });
            if (!tokens.length) return this._escapeHtml(source);
            const escaped = tokens.map(function (t) { return t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); }).join('|');
            const re      = new RegExp(escaped, 'ig');
            let out     = '', cursor = 0, match;
            while ((match = re.exec(source)) !== null) {
                out    += this._escapeHtml(source.slice(cursor, match.index));
                out    += '<span class="pg-search-match">' + this._escapeHtml(match[0]) + '</span>';
                cursor  = match.index + match[0].length;
            }
            out += this._escapeHtml(source.slice(cursor));
            return out;
        },

        // Highlight visible text rendered by a template, including text nodes directly under the cell.
        _highlightTemplateCell: function (td, term) {
            const self    = this;
            const tokens  = (term || '').trim().split(' ').filter(function (t) { return t.length > 0; });
            if (!tokens.length) return;
            const escaped = tokens.map(function (t) { return t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); }).join('|');
            const re = new RegExp(escaped, 'ig');
            self._highlightTextNodes(td, re);
        },

        // Walk all text nodes inside el and wrap matches with <span class="pg-search-match">.
        _highlightTextNodes: function (el, re) {
            const self = this;
            Array.prototype.slice.call(el.childNodes).forEach(function (node) {
                if (node.nodeType === 3 /* TEXT_NODE */) {
                    const text = node.nodeValue;
                    if (!text) return;
                    re.lastIndex = 0;
                    if (!re.test(text)) { re.lastIndex = 0; return; }
                    re.lastIndex = 0;
                    const frag = document.createDocumentFragment();
                    let last = 0, match;
                    while ((match = re.exec(text)) !== null) {
                        if (match.index > last) {
                            frag.appendChild(document.createTextNode(text.slice(last, match.index)));
                        }
                        const span = document.createElement('span');
                        span.className = 'pg-search-match';
                        span.textContent = match[0];
                        frag.appendChild(span);
                        last = match.index + match[0].length;
                    }
                    if (last < text.length) frag.appendChild(document.createTextNode(text.slice(last)));
                    re.lastIndex = 0;
                    el.replaceChild(frag, node);
                } else if (node.nodeType === 1 /* ELEMENT_NODE */ && !node.classList.contains('pg-search-match')) {
                    self._highlightTextNodes(node, re);
                }
            });
        },

        _escapeHtml: function (str) {
            return String(str)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;');
        },

        /**
         * Resolve a template by selector (e.g. '#myTemplate'), cache the raw HTML,
         * then replace every {{FieldName}} token with the matching value from dataItem.
         * Supports {{#if FieldName}}...{{/if}} for conditional blocks (truthy check).
         */
        _compileTemplate: function (selector, dataItem) {
            if (typeof selector === 'function') {
                return String(selector.call(this._el, dataItem) || '');
            }
            const id = selector.charAt(0) === '#' ? selector.slice(1) : selector;
            if (this._templateCache[id] === undefined) {
                const el = document.getElementById(id);
                this._templateCache[id] = el ? el.innerHTML.trim() : '';
            }
            let html = this._templateCache[id];
            // Process {{#if Field}}...{{/if}} blocks first
            html = html.replace(/\{\{#if\s+(\w+)\}\}([\s\S]*?)\{\{\/if\}\}/g, function (_, key, content) {
                const val = dataItem[key];
                return (val && val !== 'false' && val !== '0') ? content : '';
            });
            // Replace remaining {{Field}} tokens
            return html.replace(/\{\{(\w+)\}\}/g, function (_, key) {
                const val = dataItem[key];
                return val == null ? '' : String(val);
            });
        },

        _handleCopyableDblClick: function (e) {
            const copyable = e.target && e.target.closest ? e.target.closest('.copyable') : null;
            if (!copyable || typeof window.gtxCopyElement !== 'function') return false;

            const value = String(copyable.getAttribute('data-copy-value') || copyable.textContent || '').trim();
            if (!value) return false;

            e.preventDefault();
            e.stopPropagation();
            window.gtxCopyElement(copyable);
            return true;
        },

        _handleRowClick: function (tr, item, e) {
            const self = this;
            if (e.target && e.target.closest && e.target.closest('.copyable')) return;
            const wasSelected = self._selectedRows.has(tr);

            if (!self._opts.multiSelect) {
                // Single-select: deselect the previously selected row
                self._selectedRows.forEach(function (row) {
                    if (row !== tr) {
                        row.classList.remove('pg-row-selected');
                        self._trigger('rowDeselect', { dataItem: self._rowMap.get(row), rowElement: row });
                    }
                });
                self._selectedRows.clear();
            }

            if (wasSelected && self._opts.multiSelect) {
                // Toggle off in multi-select mode
                tr.classList.remove('pg-row-selected');
                self._selectedRows.delete(tr);
                self._trigger('rowDeselect', { dataItem: item, rowElement: tr });
            } else {
                tr.classList.add('pg-row-selected');
                self._selectedRows.add(tr);
                self._trigger('rowSelect', { dataItem: item, rowElement: tr });
            }

            self._trigger('rowClick', { dataItem: item, rowElement: tr, event: e });
            self._trigger('selectionChange', { selected: self.getSelectedItems() });
        },

        // ── Grouping ──────────────────────────────────────────────────────────

        /**
         * Build the sort state that includes group fields first (ascending),
         * then any user-chosen sort excluding the grouped fields.
         */
        _getEffectiveSort: function () {
            const self = this;
            if (!self._groupState.length) return self._sortState;
            const groupSort = self._groupState.map(function (f) { return { field: f, dir: 'asc' }; });
            const userSort  = self._sortState.filter(function (s) {
                return self._groupState.indexOf(s.field) === -1;
            });
            return groupSort.concat(userSort);
        },

        /**
         * Convert a flat sorted array into a nested group tree based on _groupState.
         * Each node: { _isGroup, _field, _value, _children, _count }
         */
        _applyGroup: function (data) {
            const self = this;

            function groupBy(rows, fields) {
                if (!fields.length) return rows;
                const field = fields[0], remaining = fields.slice(1);
                const order = [], map = {};
                rows.forEach(function (r) {
                    const key = self._textOf(field, r[field]);
                    if (!Object.prototype.hasOwnProperty.call(map, key)) { map[key] = []; order.push(key); }
                    map[key].push(r);
                });
                return order.map(function (key) {
                    const children = groupBy(map[key], remaining);
                    return { _isGroup: true, _field: field, _value: key, _children: children, _count: map[key].length };
                });
            }

            return groupBy(data, self._groupState);
        },

        /**
         * Recursively render group header rows and their leaf data rows into frag.
         */
        _renderGroupRows: function (groups, cols, frag, depth) {
            const self = this;
            const titleMap = {};
            self._opts.columns.forEach(function (c) { titleMap[c.field] = c.title || c.field; });

            groups.forEach(function (group) {
                const groupKey    = group._field + ':' + group._value + ':' + depth;
                const isCollapsed = self._collapsedGroups.has(groupKey);

                // Group header row
                const gtr = document.createElement('tr');
                gtr.className = 'pg-group-row';

                const gtd = document.createElement('td');
                gtd.colSpan = cols.length;
                gtd.style.paddingLeft = (0.65 + depth * 1.5) + 'rem';
                gtd.innerHTML =
                    '<span class="pg-group-toggle"><i class="bi bi-chevron-'
                    + (isCollapsed ? 'right' : 'down') + '"></i></span>'
                    + '<span class="pg-group-label">' + self._escapeHtml(titleMap[group._field] || group._field) + ': </span>'
                    + '<span class="pg-group-value">' + self._escapeHtml(group._value || '(blank)') + '</span>'
                    + '<span class="pg-group-count">' + group._count + '</span>';

                gtr.appendChild(gtd);
                gtr.addEventListener('click', function () {
                    if (self._collapsedGroups.has(groupKey)) { self._collapsedGroups.delete(groupKey); }
                    else { self._collapsedGroups.add(groupKey); }
                    self._renderBody();
                });
                frag.appendChild(gtr);

                if (isCollapsed) return;

                // Render children — either sub-groups or leaf data rows
                if (group._children.length && group._children[0] && group._children[0]._isGroup) {
                    self._renderGroupRows(group._children, cols, frag, depth + 1);
                } else {
                    group._children.forEach(function (item, idx) {
                        const tr = document.createElement('tr');
                        tr.className = (self._opts.alternateRows && idx % 2 !== 0) ? 'pg-row-odd' : 'pg-row-even';
                        self._applyDataRowPresentation(tr, item);

                        tr.addEventListener('click',       function (e) { self._handleRowClick(tr, item, e); });
                        tr.addEventListener('dblclick',    function (e) {
                            if (self._handleCopyableDblClick(e)) return;
                            self._trigger('rowDblClick', { dataItem: item, rowElement: tr, event: e });
                        });
                        tr.addEventListener('contextmenu', function (e) { self._trigger('rowContextMenu', { dataItem: item, rowElement: tr, event: e }); });

                        cols.forEach(function (col, colIdx) {
                            const td  = document.createElement('td');
                            if (col.field === 'Selection') td.className = 'pg-col-cb';
                            let val = item[col.field];
                            if (val == null) val = '';

                            const term         = self._searchTerm;
                            const canHighlight = term && col.searchable !== false
                                && col.encoded !== false && !col.template && col.field !== 'Selection';

                            if (col.template) {
                                td.innerHTML = self._compileTemplate(col.template, item);
                                if (term && col.searchable !== false) self._highlightTemplateCell(td, term);
                            } else if (col.encoded === false) {
                                td.innerHTML = String(val);
                                if (term && col.searchable !== false) self._highlightTemplateCell(td, term);
                            } else if (canHighlight) {
                                td.innerHTML = self._highlightMatch(String(val), term);
                            } else {
                                td.textContent = String(val);
                            }

                            const cellClass = typeof col.cellClass === 'function'
                                ? col.cellClass.call(self._el, item, val, col)
                                : col.cellClass;
                            if (cellClass) td.className += (td.className ? ' ' : '') + cellClass;

                            td.addEventListener('click', function (e) {
                                self._trigger('cellClick', {
                                    dataItem: item, field: col.field, value: val,
                                    cellElement: td, rowElement: tr, columnIndex: colIdx, event: e
                                });
                            });
                            td.addEventListener('dblclick', function (e) {
                                if (self._handleCopyableDblClick(e)) return;
                                e.stopPropagation();
                                self._trigger('cellDblClick', {
                                    dataItem: item, field: col.field, value: val,
                                    cellElement: td, rowElement: tr, columnIndex: colIdx, event: e
                                });
                            });

                            tr.appendChild(td);
                        });

                        self._rowMap.set(tr, item);
                        frag.appendChild(tr);
                        self._appendDetailRow(item, cols, frag);
                    });
                }
            });
        },

        /**
         * Build the drag-and-drop group bar and wire up its events.
         * The bar's drop handler centralises both "add field" and "reorder chip" logic.
         */
        _buildGroupBar: function () {
            const self = this;
            const bar  = document.createElement('div');
            bar.className = 'pg-group-bar';
            self._groupBar = bar;
            self._updateGroupBar();

            bar.addEventListener('dragover', function (e) {
                const types = e.dataTransfer.types;
                if ([].indexOf.call(types, 'text/add-field') !== -1 || [].indexOf.call(types, 'text/add-group-idx') !== -1) {
                    e.preventDefault();
                    e.dataTransfer.dropEffect = [].indexOf.call(types, 'text/add-group-idx') !== -1 ? 'move' : 'copy';
                }
                bar.classList.add('pg-drag-over');
            });
            bar.addEventListener('dragleave', function (e) {
                if (!bar.contains(e.relatedTarget)) { bar.classList.remove('pg-drag-over'); }
            });
            bar.addEventListener('drop', function (e) {
                e.preventDefault();
                bar.classList.remove('pg-drag-over');
                bar.querySelectorAll('.pg-group-chip').forEach(function (c) { c.classList.remove('pg-drag-over-chip'); });

                const field   = e.dataTransfer.getData('text/add-field');
                const fromIdx = parseInt(e.dataTransfer.getData('text/add-group-idx'), 10);
                const targetChip = e.target.closest ? e.target.closest('.pg-group-chip') : null;
                let toIdx   = targetChip ? parseInt(targetChip.dataset.idx, 10) : NaN;

                if (field && self._groupState.indexOf(field) === -1) {
                    // New column dropped → insert at target chip position or append
                    if (!isNaN(toIdx)) { self._groupState.splice(toIdx, 0, field); }
                    else { self._groupState.push(field); }
                    self._collapsedGroups.clear();
                    self._page = 1;
                    self._renderAll();
                    self._trigger('groupChange', { groups: self._groupState.slice() });
                } else if (!isNaN(fromIdx)) {
                    // Chip reorder
                    if (isNaN(toIdx)) { toIdx = self._groupState.length - 1; }
                    if (fromIdx !== toIdx) {
                        const moved = self._groupState.splice(fromIdx, 1)[0];
                        self._groupState.splice(toIdx, 0, moved);
                        self._collapsedGroups.clear();
                        self._page = 1;
                        self._renderAll();
                        self._trigger('groupChange', { groups: self._groupState.slice() });
                    }
                }
            });

            return bar;
        },

        /** Re-render the group bar chips from the current _groupState. */
        _updateGroupBar: function () {
            const self = this;
            if (!self._groupBar) return;
            const bar = self._groupBar;
            bar.innerHTML = '';

            const lbl = document.createElement('span');
            lbl.className = 'pg-group-bar-label text-shadow';
            lbl.innerHTML = '<i class="bi bi-collection-fill"></i>Group by:';
            bar.appendChild(lbl);

            if (!self._groupState.length) {
                const ph = document.createElement('span');
                ph.className = 'pg-group-bar-placeholder text-shadow';
                ph.innerHTML = '<i class="bi bi-arrow-bar-down"></i>Drag a column header here to group rows';
                bar.appendChild(ph);
                return;
            }

            const titleMap = {};
            self._opts.columns.forEach(function (c) { titleMap[c.field] = c.title || c.field; });

            self._groupState.forEach(function (field, idx) {
                const chip = document.createElement('span');
                chip.className     = 'pg-group-chip';
                chip.draggable     = true;
                chip.dataset.field = field;
                chip.dataset.idx   = String(idx);
                chip.innerHTML =
                    '<i class="bi bi-grip-vertical" style="color:#c7d4f8;font-size:0.68rem"></i>'
                    + '<span class="pg-group-chip-label">' + self._escapeHtml(titleMap[field] || field) + '</span>'
                    + '<button type="button" class="pg-group-chip-remove" title="Remove group"><i class="bi bi-x"></i></button>';

                chip.querySelector('.pg-group-chip-remove').addEventListener('click', function (e) {
                        e.stopPropagation();
                        self._groupState.splice(idx, 1);
                        self._collapsedGroups.clear();
                        self._page = 1;
                        self._renderAll();
                        self._trigger('groupChange', { groups: self._groupState.slice() });
                    });

                chip.addEventListener('dragstart', function (e) {
                    e.dataTransfer.setData('text/add-group-idx', String(idx));
                    e.dataTransfer.effectAllowed = 'move';
                    e.stopPropagation();
                });
                chip.addEventListener('dragover', function (e) {
                    const types = e.dataTransfer.types;
                    if ([].indexOf.call(types, 'text/add-group-idx') !== -1 || [].indexOf.call(types, 'text/add-field') !== -1) {
                        e.preventDefault();
                        chip.classList.add('pg-drag-over-chip');
                    }
                });
                chip.addEventListener('dragleave', function () { chip.classList.remove('pg-drag-over-chip'); });

                bar.appendChild(chip);
            });
        },

        _recordInfoHtml: function (total, from, to, trailingHtml) {
            const recordType = $.extend({}, defaults.recordType, this._opts.recordType || {});
            const singular = String(recordType.singular || 'row');
            const plural = String(recordType.plural || singular + 's');
            const iconClass = String(recordType.icon || '').trim();
            const iconHtml = iconClass
                ? '<i class="' + this._escapeHtml(iconClass) + '" aria-hidden="true"></i>'
                : '';

            if (!total) {
                const emptyText = recordType.emptyText == null
                    ? 'No ' + plural
                    : String(recordType.emptyText);
                return '<span class="pg-pager-info pg-pager-info-empty">'
                    + iconHtml + this._escapeHtml(emptyText)
                    + '</span>';
            }

            const rangeHtml = from != null && to != null
                ? '<span class="pg-pager-info-nums">' + from + '–' + to + '</span>'
                    + '<span class="pg-pager-info-of">of</span>'
                : '';
            const label = total === 1 ? singular : plural;

            return '<span class="pg-pager-info">'
                + iconHtml
                + rangeHtml
                + '<span class="pg-pager-info-total">' + total + '</span>'
                + '<span class="pg-pager-info-label">' + this._escapeHtml(label) + '</span>'
                + (trailingHtml || '')
                + '</span>';
        },

        _recordSummaryHtml: function (records, total) {
            if (typeof this._opts.recordSummary !== 'function') return '';

            let segments;
            try {
                segments = this._opts.recordSummary.call(this._el, records || [], total);
            } catch (error) {
                return '';
            }
            if (!Array.isArray(segments) || !segments.length) return '';

            const self = this;
            const content = segments.map(function (segment, index) {
                const item = segment || {};
                const count = Math.max(0, Number(item.count) || 0);
                const singular = String(item.singular || 'record');
                const plural = String(item.plural || singular + 's');
                const label = count === 1 ? singular : plural;
                const iconClass = String(item.icon || '').trim();
                const tone = String(item.tone || '').toLowerCase().replace(/[^a-z0-9_-]/g, '');
                const iconHtml = iconClass
                    ? '<i class="' + self._escapeHtml(iconClass) + '" aria-hidden="true"></i>'
                    : '';
                const segmentHtml = '<span class="pg-pager-info-segment'
                    + (tone ? ' is-' + tone : '') + '">'
                    + iconHtml
                    + '<span class="pg-pager-info-total">' + count + '</span>'
                    + '<span class="pg-pager-info-label">' + self._escapeHtml(label) + '</span>'
                    + '</span>';

                return (index ? '<span class="pg-pager-info-plus" aria-hidden="true">+</span>' : '')
                    + segmentHtml;
            }).join('');

            return '<span class="pg-pager-info pg-pager-info-summary">' + content + '</span>';
        },

        _renderRecordCount: function (total, records) {
            if (!this._footer || this._opts.showRecordCount !== true) return;
            const summaryHtml = this._recordSummaryHtml(records, total);
            this._footer.innerHTML = '<div class="pg-pager">'
                + (summaryHtml || this._recordInfoHtml(total))
                + '</div>';
        },

        _renderPager: function (total, from, to) {
            const self       = this;
            if (!self._footer || self._opts.pageable === false) return;
            const totalPages = Math.max(1, Math.ceil(total / self._pageSize));
            const cur        = self._page;

            const pSet = new Set([1, totalPages]);
            [cur - 1, cur, cur + 1].forEach(function (p) {
                if (p >= 1 && p <= totalPages) pSet.add(p);
            });
            const pages = Array.from(pSet).sort(function (a, b) { return a - b; });

            let btns = '', prev = 0;
            pages.forEach(function (p) {
                if (p - prev > 1) btns += '<span class="pg-pager-ellipsis">…</span>';
                btns += '<button class="pg-pager-num shadow' + (p === cur ? ' pg-pager-active' : '')
                    + '" data-page="' + p + '">' + p + '</button>';
                prev = p;
            });

            const infoHtml = self._recordInfoHtml(
                total,
                from,
                to,
                total ? '<span class="pg-pager-info-page">' + cur + ' / ' + totalPages + '</span>' : ''
            );

            const atFirst = cur <= 1;
            const atLast  = cur >= totalPages;

            self._footer.innerHTML = '<div class="pg-pager">'
                + infoHtml
                + '<nav class="pg-pager-nav shadow">'
                + '<button class="pg-pager-arrow shadow" data-page="1" title="First page"' + (atFirst ? ' disabled' : '') + '><i class="bi bi-chevron-double-left"></i></button>'
                + '<button class="pg-pager-arrow shadow" data-page="' + (cur - 1) + '" title="Previous page"' + (atFirst ? ' disabled' : '') + '><i class="bi bi-chevron-left"></i></button>'
                + btns
                + '<button class="pg-pager-arrow shadow" data-page="' + (cur + 1) + '" title="Next page"'  + (atLast  ? ' disabled' : '') + '><i class="bi bi-chevron-right"></i></button>'
                + '<button class="pg-pager-arrow shadow" data-page="' + totalPages + '" title="Last page"' + (atLast  ? ' disabled' : '') + '><i class="bi bi-chevron-double-right"></i></button>'
                + '</nav></div>';

            self._footer.querySelectorAll('[data-page]:not([disabled])').forEach(function (btn) {
                btn.addEventListener('click', function () {
                    const p = parseInt(this.dataset.page, 10);
                    if (p >= 1 && p <= totalPages && p !== cur) {
                        self._page = p;
                        self._renderBody();
                        self._trigger('pageChange', { page: p, pageSize: self._pageSize, total: total });
                    }
                });
            });
        },

        // ── Export to Excel (CSV) ─────────────────────────────────────────────
        /**
         * Export all currently filtered/searched/sorted rows to a UTF-8 CSV file
         * that Excel opens natively. Template and encoded:false columns export as
         * plain text (HTML stripped). Fires 'addgrid:exporttoexcel' on completion.
         */
        _exportToExcel: function () {
            const self = this;
            const cols = self._opts.columns.filter(function (c) {
                return !c.hidden && !c.template && c.field !== 'Selection';
            });

            // All data matching current filters/search/sort — not just the visible page
            const data = self._applySort(self._applySearch(self._applyFilter(self._raw)));

            // SpreadsheetML — opened natively by Excel, no library required
            let xml = '<?xml version="1.0" encoding="UTF-8"?>\n'
                    + '<?mso-application progid="Excel.Sheet"?>\n'
                    + '<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet"'
                    + ' xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">\n'
                    + '<Styles>'
                    + '<Style ss:ID="h"><Font ss:Bold="1"/><Interior ss:Color="#D9E1F2" ss:Pattern="Solid"/></Style>'
                    + '<Style ss:ID="e"><Interior ss:Color="#FFFFFF" ss:Pattern="Solid"/></Style>'
                    + '<Style ss:ID="o"><Interior ss:Color="#F2F4F8" ss:Pattern="Solid"/></Style>'
                    + '</Styles>\n'
                    + '<Worksheet ss:Name="Sheet1"><Table>\n';

            // Header row
            xml += '<Row>';
            cols.forEach(function (c) {
                xml += '<Cell ss:StyleID="h"><Data ss:Type="String">'
                    + self._xmlEscape(c.title != null ? c.title : c.field)
                    + '</Data></Cell>';
            });
            xml += '</Row>\n';

            // Data rows — alternate even (e) / odd (o) row styles
            data.forEach(function (item, idx) {
                const rowStyle = idx % 2 === 0 ? 'e' : 'o';
                xml += '<Row>';
                cols.forEach(function (col) {
                    let val = item[col.field];
                    if (val == null) val = '';
                    if (col.encoded === false && String(val).indexOf('<') !== -1) {
                        const d = document.createElement('div');
                        d.innerHTML = String(val);
                        val = d.textContent || d.innerText || '';
                    }
                    val = String(val).trim();
                    const isNum = val !== '' && !isNaN(Number(val));
                    xml += '<Cell ss:StyleID="' + rowStyle + '"><Data ss:Type="' + (isNum ? 'Number' : 'String') + '">'
                        + (isNum ? val : self._xmlEscape(val))
                        + '</Data></Cell>';
                });
                xml += '</Row>\n';
            });

            xml += '</Table></Worksheet></Workbook>';

            const blob     = new Blob([xml], { type: 'application/vnd.ms-excel;charset=utf-8;' });
            const fileName = (self._opts.exportFileName || 'export') + '.xls';
            const url      = URL.createObjectURL(blob);
            const a        = document.createElement('a');
            a.href          = url;
            a.download      = fileName;
            a.style.display = 'none';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);

            self._trigger('exportToExcel', { fileName: fileName, rowCount: data.length });
        },

        // ── Export to PDF ─────────────────────────────────────────────────────
        /**
         * Render all currently filtered/searched/sorted rows into a styled HTML page
         * inside a hidden <iframe> and invoke the browser print dialog.
         * The user can choose "Save as PDF" (or any installed PDF printer).
         * Template and encoded:false columns export as plain text.
         * Fires 'addgrid:exporttopdf' after the dialog opens.
         */
        _exportToPdf: function () {
            const self = this;
            const cols = self._opts.columns.filter(function (c) {
                return !c.hidden && !c.template && c.field !== 'Selection';
            });

            const data     = self._applySort(self._applySearch(self._applyFilter(self._raw)));
            const title    = self._opts.exportFileName || 'export';
            const colWidth = cols.length ? Math.floor(100 / cols.length) + '%' : 'auto';

            let html = '<!DOCTYPE html><html><head><meta charset="UTF-8">'
                     + '<title>' + self._xmlEscape(title) + '</title>'
                     + '<style>'
                     + 'body{font-family:Arial,sans-serif;font-size:11px;margin:24px;color:#1e293b}'
                     + 'h2{font-size:13px;margin:0 0 10px;color:#334155}'
                     + 'table{border-collapse:collapse;width:100%;table-layout:fixed}'
                     + 'th{background:#D9E1F2;font-weight:700;border:1px solid #b0bec5;'
                     + '   padding:5px 8px;text-align:left;font-size:9px;text-transform:uppercase;'
                     + '   letter-spacing:.04em;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}'
                     + 'td{border:1px solid #dee2e6;padding:4px 8px;overflow:hidden;'
                     + '   text-overflow:ellipsis;white-space:nowrap}'
                     + 'tr.pg-odd td{background:#F2F4F8}'
                     + 'tr.pg-even td{background:#FFFFFF}'
                     + 'col{width:' + colWidth + '}'
                     + '@page{margin:1.5cm;size:landscape}'
                     + '@media print{body{margin:0}}'
                     + '</style></head><body>'
                     + '<h2>' + self._xmlEscape(title) + '</h2>'
                     + '<table><colgroup>';

            cols.forEach(function () { html += '<col>'; });
            html += '</colgroup><thead><tr>';
            cols.forEach(function (c) {
                html += '<th>' + self._xmlEscape(c.title != null ? c.title : c.field) + '</th>';
            });
            html += '</tr></thead><tbody>';

            data.forEach(function (item, idx) {
                html += '<tr class="' + (idx % 2 === 0 ? 'pg-even' : 'pg-odd') + '">';
                cols.forEach(function (col) {
                    let val = item[col.field];
                    if (val == null) val = '';
                    if (col.encoded === false && String(val).indexOf('<') !== -1) {
                        const d = document.createElement('div');
                        d.innerHTML = String(val);
                        val = d.textContent || d.innerText || '';
                    }
                    html += '<td>' + self._xmlEscape(String(val).trim()) + '</td>';
                });
                html += '</tr>';
            });

            html += '</tbody></table></body></html>';

            const blob  = new Blob([html], { type: 'text/html;charset=utf-8' });
            const blobUrl = URL.createObjectURL(blob);

            const iframe = document.createElement('iframe');
            iframe.style.cssText = 'position:fixed;top:-9999px;left:-9999px;width:1px;height:1px;border:none';
            document.body.appendChild(iframe);

            iframe.onload = function () {
                iframe.contentWindow.focus();
                iframe.contentWindow.print();
                setTimeout(function () {
                    document.body.removeChild(iframe);
                    URL.revokeObjectURL(blobUrl);
                }, 1000);
            };
            iframe.src = blobUrl;

            self._trigger('exportToPdf', { rowCount: data.length });
        },

        _xmlEscape: function (str) {
            return String(str)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;');
        }
    };

    // ════════════════════════════════════════════════════════════════════════
    //  jQuery plugin bridge
    // ════════════════════════════════════════════════════════════════════════
    $.fn.addGrid = function (optionsOrMethod) {
        const args      = Array.prototype.slice.call(arguments, 1);
        let returnVal = this;

        this.each(function () {
            const $el      = $(this);
            let instance = $el.data(DATA_KEY);

            if (typeof optionsOrMethod === 'string') {
                if (!instance) {
                    $.error('addGrid has not been initialized on this element.');
                    return;
                }
                if (typeof instance[optionsOrMethod] !== 'function') {
                    $.error('addGrid: method "' + optionsOrMethod + '" does not exist.');
                    return;
                }
                const result = instance[optionsOrMethod].apply(instance, args);
                if (result !== undefined) { returnVal = result; return false; }
            } else {
                if (!instance) {
                    instance = new AddGrid($el, optionsOrMethod || {});
                    $el.data(DATA_KEY, instance);
                    if (optionsOrMethod && Array.isArray(optionsOrMethod.data)) {
                        instance._trigger('dataBound', {
                            data: instance._raw,
                            total: instance._raw.length
                        });
                    }
                }
            }
        });

        return returnVal;
    };

}(jQuery));
