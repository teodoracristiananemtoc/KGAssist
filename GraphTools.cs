
using File = System.IO.File;


namespace GraphTools
{
    [McpServerToolType]
    public class GraphTools
    {
        private readonly IServiceProvider serviceProvider;

        public GraphTools(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        [McpServerTool, Description("Get Repositories")]
        public async Task<string> GetRepositories()
        {
            using (var scope = serviceProvider.CreateScope())
            {

                var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                var client = httpClientFactory.CreateClient();

                var url = "http://localhost:7200/repositories";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
        }

        [McpServerTool]
        [Description("Convert any JSON file dynamically to valid RDF Turtle (GraphDB safe)")]
        public async Task<string> ConvertJsonToTurtle(string jsonFilePath, string outputTurtlePath)
        {
            if (!File.Exists(jsonFilePath))
                throw new FileNotFoundException($"Input JSON file not found: {jsonFilePath}");
            string baseNs = "http://example.org/resource/";
            var json = await File.ReadAllTextAsync(jsonFilePath);
            var ttl = ConvertJsonToTurtle(json);
            await File.WriteAllTextAsync(outputTurtlePath, ttl.ToString(), new UTF8Encoding(false)); // fără BOM
            return $" JSON successfully converted to RDF Turtle: {outputTurtlePath}";
        }

        static string ConvertJsonToTurtle(string json)
        {
            string baseNs = "http://example.org/resource/";
            var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
            var ttl = new StringBuilder();
            ttl.AppendLine("@prefix ex: <" + baseNs + "> .");
            ttl.AppendLine("@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .");
            ttl.AppendLine("@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .");
            ttl.AppendLine();
            int rootIndex = 0;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                    ProcessObject(el, ttl, "Root", rootIndex++);
            }
            else
            {
                ProcessObject(doc.RootElement, ttl, "Root", 0);
            }

            return ttl.ToString();
        }

        static string ProcessObject(JsonElement obj, StringBuilder ttl, string typeName, int index)
        {
            string subject = $"ex:{Sanitize(typeName)}_{index}";
            var props = new List<string>();
            var children = new List<string>();

            foreach (var prop in obj.EnumerateObject())
            {
                string predicate = $"ex:{Sanitize(prop.Name)}";
                var value = prop.Value;

                if (value.ValueKind == JsonValueKind.Object)
                {
                    string childSubject = $"ex:{Sanitize(prop.Name)}_{Guid.NewGuid():N}";
                    ProcessObject(value, ttl, prop.Name, index);
                    props.Add($"{predicate} {childSubject}");
                }
                else if (value.ValueKind == JsonValueKind.Array)
                {
                    int i = 0;
                    foreach (var child in value.EnumerateArray())
                    {
                        string childSubject = $"ex:{Sanitize(prop.Name)}_{Guid.NewGuid():N}";
                        ProcessObject(child, ttl, prop.Name, i++);
                        props.Add($"{predicate} {childSubject}");
                    }
                }
                else
                {
                    string lit = SerializeValue(value);
                    props.Add($"{predicate} {lit}");
                }
            }

            ttl.AppendLine($"{subject} a ex:{Sanitize(typeName)} ;");
            for (int i = 0; i < props.Count; i++)
            {
                string end = (i == props.Count - 1) ? " ." : " ;";
                ttl.AppendLine($"    {props[i]}{end}");
            }
            ttl.AppendLine();

            return subject;
        }

        static string SerializeValue(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    if (DateTime.TryParse(value.GetString(), out _))
                        return $"\"{Escape(value.GetString())}\"^^xsd:dateTime";
                    return $"\"{Escape(value.GetString())}\"";
                case JsonValueKind.Number:
                    if (value.TryGetInt64(out long i))
                        return $"\"{i}\"^^xsd:integer";
                    if (value.TryGetDouble(out double d))
                        return $"\"{d}\"^^xsd:decimal";
                    return $"\"{Escape(value.ToString())}\"";
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return $"\"{value.GetBoolean().ToString().ToLower()}\"^^xsd:boolean";
                default:
                    return $"\"{Escape(value.ToString())}\"";
            }
        }

        static string Sanitize(string s)
            => Regex.Replace(s, @"[^A-Za-z0-9_]", "_");

        static string Escape(string text)
            => text.Replace("\"", "\\\"");


        public async Task<string> GenerateGraphDbRepoConfig(string repositoryName)
        {
            try
            {
            string ttlTemplate = $@"
            # Auto-generated GraphDB repository configuration
            @prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#>.
            @prefix rep: <http://www.openrdf.org/config/repository#>.
            @prefix sr: <http://www.openrdf.org/config/repository/sail#>.
            @prefix sail: <http://www.openrdf.org/config/sail#>.
            @prefix graphdb: <http://www.ontotext.com/config/graphdb#>.

            [] a rep:Repository ;
                rep:repositoryID ""{repositoryName}"" ;
                rdfs:label ""Repository {repositoryName}"" ;
                rep:repositoryImpl [
                    rep:repositoryType ""graphdb:SailRepository"" ;
                    sr:sailImpl [
                        sail:sailType ""graphdb:Sail"" ;

                        graphdb:read-only ""false"" ;

                        # Inference and Validation
                        graphdb:ruleset ""rdfsplus-optimized"" ;
                        graphdb:disable-sameAs ""true"" ;
                        graphdb:check-for-inconsistencies ""false"" ;

                        # Indexing
                        graphdb:entity-id-size ""32"" ;
                        graphdb:enable-context-index ""false"" ;
                        graphdb:enablePredicateList ""true"" ;
                        graphdb:enable-fts-index ""false"" ;
                        graphdb:fts-indexes (""default"" ""iri"") ;
                        graphdb:fts-string-literals-index ""default"" ;
                        graphdb:fts-iris-index ""none"" ;

                        # Queries and Updates
                        graphdb:query-timeout ""0"" ;
                        graphdb:throw-QueryEvaluationException-on-timeout ""false"" ;
                        graphdb:query-limit-results ""0"" ;

                        # Base config
                        graphdb:base-URL ""http://example.org/{repositoryName}#"" ;
                        graphdb:defaultNS """" ;
                        graphdb:imports """" ;
                        graphdb:repository-type ""file-repository"" ;
                        graphdb:storage-folder ""storage/{repositoryName}"" ;
                        graphdb:entity-index-size ""10000000"" ;
                        graphdb:in-memory-literal-properties ""true"" ;
                        graphdb:enable-literal-index ""true"" ;
                    ]
                ].
            ";
                string tempPath = Path.Combine(Path.GetTempPath(), $"{repositoryName}-config.ttl");
                await File.WriteAllTextAsync(tempPath, ttlTemplate, Encoding.UTF8);

                return $"Config file created successfully at: {tempPath}";
            }
            catch (Exception ex)
            {
                return $"Error generating config file: {ex.Message}";
            }
        }


        [McpServerTool, Description("Generate a GraphDB repository config TTL file and create the repository in GraphDB")]
        public async Task<string> CreateGraphDbRepository(string repositoryName, string outputPath)
        {
            try
            {
                string ttlContent = $@"
                # Auto-generated GraphDB repository configuration
                @prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#>.
                @prefix rep: <http://www.openrdf.org/config/repository#>.
                @prefix sr: <http://www.openrdf.org/config/repository/sail#>.
                @prefix sail: <http://www.openrdf.org/config/sail#>.
                @prefix graphdb: <http://www.ontotext.com/config/graphdb#>.

                [] a rep:Repository ;
                    rep:repositoryID ""{repositoryName}"" ;
                    rdfs:label ""Repository {repositoryName}"" ;
                    rep:repositoryImpl [
                        rep:repositoryType ""graphdb:SailRepository"" ;
                        sr:sailImpl [
                            sail:sailType ""graphdb:Sail"" ;

                            graphdb:read-only ""false"" ;

                            # Inference and Validation
                            graphdb:ruleset ""rdfsplus-optimized"" ;
                            graphdb:disable-sameAs ""true"" ;
                            graphdb:check-for-inconsistencies ""false"" ;

                            # Indexing
                            graphdb:entity-id-size ""32"" ;
                            graphdb:enable-context-index ""false"" ;
                            graphdb:enablePredicateList ""true"" ;
                            graphdb:enable-fts-index ""false"" ;
                            graphdb:fts-indexes (""default"" ""iri"") ;
                            graphdb:fts-string-literals-index ""default"" ;
                            graphdb:fts-iris-index ""none"" ;

                            # Queries and Updates
                            graphdb:query-timeout ""0"" ;
                            graphdb:throw-QueryEvaluationException-on-timeout ""false"" ;
                            graphdb:query-limit-results ""0"" ;

                            # Base config
                            graphdb:base-URL ""http://example.org/{repositoryName}#"" ;
                            graphdb:defaultNS """" ;
                            graphdb:imports """" ;
                            graphdb:repository-type ""file-repository"" ;
                            graphdb:storage-folder ""storage/{repositoryName}"" ;
                            graphdb:entity-index-size ""10000000"" ;
                            graphdb:in-memory-literal-properties ""true"" ;
                            graphdb:enable-literal-index ""true"" ;
                        ]
                    ].
                ";

              
                string ttlFilePath = Path.Combine(outputPath, $"{repositoryName}-config.ttl");
                await System.IO.File.WriteAllTextAsync(ttlFilePath, ttlContent, Encoding.UTF8);

             
                using var scope = serviceProvider.CreateScope();
                var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                var client = httpClientFactory.CreateClient();

                using var form = new MultipartFormDataContent();
                var configFile = new ByteArrayContent(await System.IO.File.ReadAllBytesAsync(ttlFilePath));
                configFile.Headers.ContentType = new MediaTypeHeaderValue("application/x-turtle");
                form.Add(configFile, "config", Path.GetFileName(ttlFilePath));

                string url = "http://localhost:7200/rest/repositories";
                var response = await client.PostAsync(url, form);

                if (!response.IsSuccessStatusCode)
                {
                    string err = await response.Content.ReadAsStringAsync();
                    return $" Failed to create repository '{repositoryName}': {response.StatusCode}\n{err}";
                }

                return $" Repository '{repositoryName}' created successfully.\nConfig file: {ttlFilePath}";
            }
            catch (Exception ex)
            {
                return $" Error: {ex.Message}";
            }
        }

        [McpServerTool, Description("Upload a TTL data file to an existing GraphDB repository")]
        public async Task<string> UploadTurtleToRepository(string repositoryName, string dataTtlPath)
        {
            if (!File.Exists(dataTtlPath))
                return $" TTL file not found at: {dataTtlPath}";

            try
            {
                using var scope = serviceProvider.CreateScope();
                var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                var client = httpClientFactory.CreateClient();


                var ttlData = await File.ReadAllTextAsync(dataTtlPath);
                var content = new StringContent(ttlData, Encoding.UTF8, "application/x-turtle");

                var response = await client.PostAsync($"http://localhost:7200/repositories/{repositoryName}/statements", content);

                if (!response.IsSuccessStatusCode)
                {
                    string err = await response.Content.ReadAsStringAsync();
                    return $" Failed to upload TTL file to repository '{repositoryName}': {response.StatusCode}\n{err}";
                }

                return $" TTL file uploaded successfully to repository '{repositoryName}'!";
            }
            catch (Exception ex)
            {
                return $" Error: {ex.Message}";
            }
        }

            [McpServerTool, Description("Generate  SPARQL query from natural language")]
            public async Task<string> GenerateAndRunSparql(string repositoryName, string ontologyPath, string naturalLanguageQuery)
            {
                if (!File.Exists(ontologyPath))
                    return $"Ontology file not found at: {ontologyPath}";

            try
            {
                var httpClient = new HttpClient();
                var ontologyData = await File.ReadAllTextAsync(ontologyPath);
                string llmPrompt = $@"
                                    You are an expert in RDF and SPARQL.
                                    Given the ontology below (Turtle format) and the user's question,
                                    generate a valid SPARQL SELECT query for GraphDB.

                                    Ontology:
                                    {ontologyData}

                                    Question:
                                    """"{{naturalLanguageQuery}}""""

                                    Return ONLY the SPARQL query,and automatically call tool executesparql.";

                return await Task.FromResult(llmPrompt);
            }
            catch (Exception ex)
            {
                return $" Error: {ex.Message}";
            }
        }

        [McpServerTool, Description("execute  SPARQL ")]
        public async Task<string> ExecuteSparql(string repositoryName, string sparqlQuery)
        {


            try
            {
                var httpClient = new HttpClient();
                var queryContent = new StringContent($"query={Uri.EscapeDataString(sparqlQuery)}", Encoding.UTF8, "application/x-www-form-urlencoded");
                var graphDbResponse = await httpClient.PostAsync($"http://localhost:7200/repositories/{repositoryName}", queryContent);

                if (!graphDbResponse.IsSuccessStatusCode)
                {
                    string err = await graphDbResponse.Content.ReadAsStringAsync();
                    return $" Failed to execute SPARQL query on repository '{repositoryName}': {graphDbResponse.StatusCode}\n{err}";
                }

                string result = await graphDbResponse.Content.ReadAsStringAsync();
                return $"SPARQL query executed successfully.\n\n---\nQuery:\n{sparqlQuery}\n---\nResults:\n{result}";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private string ParseResponse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var text = doc.RootElement[0].GetProperty("generated_text").GetString();
                return text ?? "No generated query.";
            }
            catch
            {
                return "Could not interpet response.";
            }
        }

    }
}


