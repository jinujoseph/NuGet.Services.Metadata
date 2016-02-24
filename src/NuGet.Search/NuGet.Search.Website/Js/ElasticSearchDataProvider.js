var ElasticSearchDataProvider = (function () {
    function ElasticSearchDataProvider(options) {
        this._options = options;
        this._esClient = jQuery.es.Client({
            host: options.host ? options.host : ElasticSearchDataProvider._defaultOptions.host
        });
    }
    ElasticSearchDataProvider.prototype.SearchRaw = function (options) {
        options = this._sanitizeSearchOptions(options);
        $.ajax({
            url: '/api/SearchRaw',
            type: 'POST',
            data: JSON.stringify(options),
            dataType: 'JSON',
            contentType: "application/json; charset=utf-8",
            success: function (responseText) {
                var response = JSON.parse(responseText);
                if (options.callback) {
                    options.callback(response, null);
                }
            },
            error: function (err) {
                if (options.callback) {
                    options.callback(null, err);
                }
            }
        });
    };
    ElasticSearchDataProvider.prototype._sanitizeSearchOptions = function (options) {
        options.index = options.index.trim();
        options.searchText = options.searchText.trim();
        options.pageSize = options.pageSize ? options.pageSize : 50;
        options.pageNumber = options.pageNumber ? options.pageNumber : 1;
        return options;
    };
    ElasticSearchDataProvider._defaultOptions = {
        host: 'test:9200'
    };
    return ElasticSearchDataProvider;
})();
//# sourceMappingURL=ElasticSearchDataProvider.js.map