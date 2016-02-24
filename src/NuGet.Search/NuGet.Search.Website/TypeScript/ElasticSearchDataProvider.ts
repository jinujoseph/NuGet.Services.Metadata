interface IElasticSearchOptions {
    index?: string;
    docType: string;
    searchText: string;
    sort?: string;
    pageSize?: number;
    pageNumber?: number;
    callback?: (response: any, err: Error) => void;
}

interface IElasticSearchResponse {
    took: number;
    timed_out: boolean;
    _shards: {
        total: number;
        successful: number;
        failed: number;
    };
    hits: {
        total: number;
        max_score: number;
        hits: Array<IElasticSearchHit>;
    }
}

interface IElasticSearchHit {
    _index: string;
    _type: string;
    _id: string;
    _score: number;
    _source: any;
}

interface IElasticSearchDataProviderOptions {
    host: string;
}

class ElasticSearchDataProvider {
    private _esClient: any;
    private _options: IElasticSearchDataProviderOptions;

    private static _defaultOptions: IElasticSearchDataProviderOptions = {
        host: 'test:9200'
    }

    constructor(options?: IElasticSearchDataProviderOptions) {
        this._options = options;
        this._esClient = jQuery.es.Client({
            host: options.host ? options.host : ElasticSearchDataProvider._defaultOptions.host
        });
    }

    public SearchRaw(options: IElasticSearchOptions): void {
        options = this._sanitizeSearchOptions(options);

        $.ajax({
            url: '/api/SearchRaw',
            type: 'POST',
            data: JSON.stringify(options),
            dataType: 'JSON',
            contentType: "application/json; charset=utf-8",
            success: function (responseText: string): void {
                var response = JSON.parse(responseText);

                if (options.callback) {
                    options.callback(response, null);
                }
            },
            error: function (err: any): void {
                if (options.callback) {
                    options.callback(null, err);
                }
            }
        });
    }

    private _sanitizeSearchOptions(options: IElasticSearchOptions): IElasticSearchOptions {
        options.index = options.index.trim();
        options.searchText = options.searchText.trim();
        options.pageSize = options.pageSize ? options.pageSize : 50;
        options.pageNumber = options.pageNumber ? options.pageNumber : 1;
        return options;
    }
}