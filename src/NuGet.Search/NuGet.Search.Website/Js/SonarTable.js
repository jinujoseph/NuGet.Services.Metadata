var MasterTable = (function () {
    function MasterTable(options) {
        var that = this;
        if (!options || options.tableId == null) {
            throw new Error('A MasterTableOptions object with at least "tableId" is required.');
        }
        this.Options = options;
        var dataSource = this.KendoUIDataSource = new kendo.data.DataSource({
            error: function (e) {
                dataSource.data([]);
            },
            pageSize: 100,
            schema: {
                data: "Data",
                total: "Total"
            },
            serverFiltering: true,
            serverPaging: true,
            transport: {
                read: {
                    url: '/api/Search',
                    type: 'POST',
                    data: function () {
                        return {
                            index: options.index,
                            searchText: encodeURIComponent($("#searchBar").val().toString()),
                            docType: options.docType,
                            sort: options.sort
                        };
                    },
                    dataType: 'json',
                    contentType: "application/json",
                    cache: false
                },
                parameterMap: function (data, operation) {
                    return JSON.stringify(data);
                },
            }
        });
        $(options.tableId).html("");
        var displayColumn = function (value, array) {
            var display = $.inArray(value, array) >= 0 ? false : true;
            return display;
        };
        var kendoGrid = this.KendoUITable = $(options.tableId).kendoGrid({
            allowCopy: true,
            change: options.rowClickEvent,
            columnMenu: true,
            columnResizeHandleWidth: 8,
            columnHide: options.columnChangedEvent,
            columnShow: options.columnChangedEvent,
            columns: [
                { field: "Id", title: "Id", width: 200, hidden: displayColumn('Id', options.activeCols) },
                { field: "RunLogs[0].RunInfo.Machine", title: "Machine", width: 200, hidden: displayColumn('Machine', options.activeCols) },
                { field: "RunLogs[0].RunInfo.RunDate", title: "Date", width: 200, hidden: displayColumn('Date', options.activeCols) },
                { field: "RunLogs[0].ToolInfo.FullName", title: "Tool", width: 200, hidden: displayColumn('Tool', options.activeCols) },
            ],
            dataBound: options.dataBound,
            dataSource: this.KendoUIDataSource,
            height: "auto",
            navigatable: true,
            pageable: {
                buttonCount: 1,
                messages: {
                    display: "Showing {0}-{1} from {2} data items."
                },
                info: true,
            },
            reorderable: true,
            resizable: true,
            selectable: true,
            scrollable: {
                virtual: true
            },
        });
    }
    return MasterTable;
})();
//# sourceMappingURL=SonarTable.js.map