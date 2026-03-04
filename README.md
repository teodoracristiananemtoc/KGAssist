# MCP GraphDB Server

Custom **Model Context Protocol (MCP) Server** for automated Knowledge Graph creation and semantic querying in GraphDB.

This project enables natural language interaction with RDF data by bridging an MCP-compatible LLM client and GraphDB through controlled SPARQL execution and repository management.

---

## 🚀 Overview

Working with Knowledge Graphs typically requires:

- Manual repository configuration  
- RDF serialization knowledge (Turtle, RDF/XML, etc.)  
- SPARQL expertise  
- Familiarity with GraphDB tools  

This MCP Server removes those barriers by acting as a controlled intermediary between an LLM and GraphDB.

Users can:

- Create GraphDB repositories programmatically  
- Convert JSON data to RDF (Turtle)  
- Upload data into a repository  
- Execute SPARQL queries  
- Ask questions in natural language  

---

## 🏗 Architecture

```
User (Natural Language)
        ↓
MCP Client (e.g., Claude Desktop)
        ↓
Custom MCP Server (.NET 8)
        ↓
GraphDB REST API
        ↓
SPARQL Execution & RDF Storage
```

- Communication: STDIO transport  
- SPARQL execution via GraphDB REST endpoints  
- LLM triggers tools, server executes logic  

---

## ✨ Features

### Repository Management
- Auto-generates `.ttl` repository configuration  
- Calls GraphDB REST API  
- Creates repositories without manual UI interaction  

### Data Transformation
- JSON → RDF (Turtle)  
- Automatic prefix handling  
- Ontology-aware mapping  

### Data Upload
- Uploads triples directly to repository  
- No manual serialization required  

### SPARQL Execution
- Natural language → SPARQL  
- Aggregation support (`COUNT`, `GROUP BY`, `ORDER BY`)  
- Multi-hop queries  
- Schema-aware refinement  

---

## 🛠 Tech Stack

- .NET 8  
- ModelContextProtocol-SemanticKernel  
- GraphDB REST API  
- SPARQL 1.1  
- RDF / Turtle  

---

## 📦 Requirements

- Running GraphDB instance (default: `http://localhost:7200`)  
- MCP-compatible client (e.g., Claude Desktop)  
- .NET 8 Runtime  

---

## 🔧 Setup

### 1️⃣ Clone Repository

```bash
git clone https://github.com/your-username/mcp-graphdb-server.git
cd mcp-graphdb-server
```

### 2️⃣ Build

```bash
dotnet build
```

### 3️⃣ Run

```bash
dotnet run
```

Or configure your MCP client to point to the compiled executable.

---

## ⚙️ MCP Client Configuration Example

```json
{
  "servers": {
    "graphdb-mcp": {
      "command": "path/to/mcp-server-executable"
    }
  }
}
```

---

 
