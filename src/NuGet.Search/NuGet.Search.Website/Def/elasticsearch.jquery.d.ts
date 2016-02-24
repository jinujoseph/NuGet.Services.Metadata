/// <reference path="./jquery.d.ts" />

interface IElasticSearchClient {
    Client: (config: any) => any;
}

interface JQueryStatic {
    es: IElasticSearchClient; 
}

//declare var elasticsearch: any;
//declare var Client: IElasticSearchClient;

//declare module "elasticsearch" {
//    export = Client;
//}