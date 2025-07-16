# QuiosqueBI - Backend API

![C#](https://img.shields.io/badge/c%23-%23239120.svg?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Postgres](https://img.shields.io/badge/postgres-%23316192.svg?style=for-the-badge&logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/docker-%230db7ed.svg?style=for-the-badge&logo=docker&logoColor=white)
![Azure](https://img.shields.io/badge/azure-%230072C6.svg?style=for-the-badge&logo=microsoftazure&logoColor=white)

Este repositório contém o backend da aplicação **QuiosqueBI**, uma plataforma de análise de dados inteligente. A API foi construída com **.NET 8 (C#)** e é responsável por toda a lógica de negócio, incluindo autenticação de usuários, processamento de arquivos, integração com IA e persistência de dados.

---

> ### 🎨 **Frontend Interativo**
> A interface desta aplicação foi construída com Vue.js 3 e está em um repositório separado.
> **[Acesse o repositório do Frontend aqui](https://github.com/jv-dias/QuiosqueBI)**

---

## 🏛️ Arquitetura e Conceito

O backend atua como o cérebro da aplicação. Ele segue uma arquitetura em camadas para garantir a separação de responsabilidades e a manutenibilidade. O fluxo principal é:

1.  **Autenticação:** Valida usuários usando um sistema seguro com **ASP.NET Core Identity** e emite **Tokens JWT** para autorizar requisições.
2.  **Recebimento de Dados:** Aceita o upload de arquivos (CSV/XLSX) e um contexto em linguagem natural do usuário.
3.  **Integração com IA:** Envia os metadados do arquivo e o contexto do usuário para a **API do Google Gemini**, que gera um plano de análise dinâmico.
4.  **Processamento e Persistência:** Executa o plano de análise, agregando e formatando os dados. Os resultados são então associados ao usuário autenticado e salvos em um banco de dados **PostgreSQL**.
5.  **Serviço de Dados:** Expõe endpoints RESTful para o frontend consumir, tanto para gerar novas análises quanto para consultar análises históricas.

## 🚀 Stack de Tecnologias

* **Framework:** .NET 8, ASP.NET Core
* **Linguagem:** C#
* **Banco de Dados:** PostgreSQL
* **ORM:** Entity Framework Core
* **Autenticação:** ASP.NET Core Identity, JWT (JSON Web Tokens)
* **Inteligência Artificial:** Google Gemini API
* **Containerização:** Docker, Docker Compose
* **Cloud:** Azure App Service, Azure Database for PostgreSQL

## 🐳 Como Rodar Localmente com Docker

A maneira mais fácil e recomendada de rodar o ambiente de desenvolvimento é usando Docker. Isso garante um ambiente consistente e elimina a necessidade de instalar o PostgreSQL localmente.

### Pré-requisitos
* [.NET 8 SDK](https://dotnet.microsoft.com/download)
* [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Passos

1.  **Clone o repositório:**
    ```sh
    git clone [https://github.com/jv-dias/QuiosqueBI-Backend.git](https://github.com/jv-dias/QuiosqueBI-Backend.git)
    cd QuiosqueBI-Backend
    ```

2.  **Configure as Variáveis de Ambiente:**
    Na pasta `QuiosqueBI.API`, crie um arquivo `appsettings.Development.json` e preencha com seus segredos. Este arquivo já está no `.gitignore` e não será enviado ao repositório.

    ```json
    {
      "ConnectionStrings": {
        "DefaultConnection": "Server=localhost;Port=5432;Database=quiosquebi_db;User Id=admindb;Password=sua_senha_forte_aqui"
      },
      "Gemini": {
        "ApiKey": "SUA_CHAVE_API_DO_GEMINI"
      },
      "Jwt": {
        "SecretKey": "SUA_CHAVE_SECRETA_SUPER_LONGA_E_SEGURA",
        "Issuer": "http://localhost:5159",
        "Audience": "http://localhost:5159"
      }
    }
    ```
    *Nota: A senha do banco (`sua_senha_forte_aqui`) deve ser a mesma que você define no arquivo `docker-compose.yml`.*

3.  **Inicie a Aplicação Completa:**
    Na raiz do projeto (`QuiosqueBI-Backend`), execute um único comando:
    ```sh
    docker-compose up --build
    ```
    Este comando irá:
    * Construir a imagem Docker da sua API.
    * Baixar e iniciar um contêiner do PostgreSQL.
    * Criar o banco de dados e aplicar as migrações automaticamente.
    * Iniciar sua API, que estará acessível em `http://localhost:5159`.

## ✨ Destaques da Arquitetura

* **Pronto para a Nuvem:** O projeto foi containerizado com Docker e implantado no **Azure**, utilizando **Azure Container Apps** para o serviço da API e **Azure Database for PostgreSQL** como banco de dados gerenciado.
* **CI/CD Automatizado:** A implantação no Azure é gerenciada por um pipeline de CI/CD com **GitHub Actions**, que constrói a imagem Docker e a publica no **Azure Container Registry (ACR)** a cada `push` na branch `main`.
* **Segurança Robusta:** Sistema completo de autenticação e autorização, garantindo que cada usuário acesse apenas seus próprios dados.
* **Processamento Otimizado:** Uso de técnicas de streaming para analisar arquivos grandes (+20.000 linhas) com baixo consumo de memória.
* **Arquitetura Limpa:** A lógica de negócio é desacoplada da camada de API através de um Service Layer, seguindo os princípios de injeção de dependência.

## 📜 Licença

Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](LICENSE.md) para mais detalhes.