# QuiosqueBI - Análise de Dados com IA

**⚠️ Importante:** Este é um projeto de **portfólio** criado para demonstrar habilidades em desenvolvimento Full-Stack com .NET e Vue.js, integração com APIs de Inteligência Artificial e implementação de sistemas de autenticação seguros.

O QuiosqueBI é uma aplicação web completa onde usuários podem se registrar, fazer o upload de arquivos de dados (CSV/XLSX), gerar análises visuais através de comandos em linguagem natural, e salvar ou revisitar seu histórico pessoal de forma segura.

---

## Conceito Principal: O Fluxo de Análise

O projeto combina uma arquitetura robusta com Inteligência Artificial para entregar uma experiência de usuário fluida e poderosa.

1.  **Registro e Login:** O usuário cria uma conta segura. O sistema utiliza **ASP.NET Core Identity** para gerenciamento de usuários e **Tokens JWT** para autenticação, garantindo que cada sessão seja validada.
2.  **Upload e Contexto:** Uma vez logado, o usuário envia um arquivo de dados e descreve seu objetivo de análise.
3.  **Inteligência Artificial em Ação:** O backend em .NET envia os metadados do arquivo e o objetivo do usuário para a **API do Google Gemini**.
4.  **Processamento e Visualização:** A IA retorna um plano de análise, que o backend executa para processar os dados e gerar os resultados visuais, que são então exibidos no frontend em Vue.js.
5.  **Persistência e Histórico:** Os resultados da análise são associados ao usuário logado e salvos em um banco de dados **PostgreSQL**. O usuário pode revisitar suas análises a qualquer momento através de sua página de histórico pessoal e segura.

---

## 🚀 Stack de Tecnologias

* **Backend:** API RESTful com **.NET 8**, **Entity Framework Core**, **ASP.NET Core Identity**.
* **Banco de Dados:** **PostgreSQL**.
* **Frontend:** Single Page Application (SPA) com **Vue 3** (Composition API) + Vite.
* **Inteligência Artificial:** Google Gemini API.
* **Autenticação:** Tokens **JWT (JSON Web Tokens)**.
* **Estilização:** Tailwind CSS.
* **Linguagens:** C#, TypeScript.

---

## 📋 Pré-requisitos

* [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
* [Node.js](https://nodejs.org/) (v18 ou superior)
* [PostgreSQL](https://www.postgresql.org/download/) com um banco de dados criado (ex: `QuiosqueBI_DB`).

---

## ⚙️ Configuração Essencial

Para a aplicação funcionar corretamente, você precisa configurar as chaves da API, a conexão com o banco e o segredo do JWT.

1.  Navegue até a pasta do backend: `cd backend`
2.  Crie um arquivo chamado `appsettings.Development.json`.
3.  Adicione as configurações abaixo, substituindo os valores de exemplo:

    ```json
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      },
      "ConnectionStrings": {
        "DefaultConnection": "Server=localhost;Port=5432;Database=QuiosqueBI_DB;User Id=postgres;Password=SUA_SENHA_DO_POSTGRES;"
      },
      "Gemini": {
        "ApiKey": "SUA_CHAVE_API_DO_GEMINI_VAI_AQUI"
      },
      "Jwt": {
        "SecretKey": "SUA_CHAVE_SECRETA_SUPER_LONGA_E_SEGURA_COM_MAIS_DE_256_BITS",
        "Issuer": "https://localhost:5001",
        "Audience": "https://localhost:5001"
      }
    }
    ```

---

## ⚡ Como Rodar o Projeto

Você precisará de dois terminais abertos, um para o backend e um para o frontend.

### Backend (.NET API)

1.  **Navegue até a pasta:**
    ```sh
    cd backend
    ```
2.  **Restaure os pacotes do .NET:**
    ```sh
    dotnet restore
    ```
3.  **Execute as Migrations do Banco de Dados:** Este comando criará todas as tabelas, incluindo as do sistema de identidade.
    ```sh
    dotnet ef database update
    ```
4.  **Execute a API:**
    ```sh
    dotnet run
    ```

### Frontend (Vue.js App)

1.  **Navegue até a pasta:**
    ```sh
    cd frontend
    ```
2.  **Instale as dependências:**
    ```sh
    npm install
    ```
3.  **Execute o servidor de desenvolvimento:**
    ```sh
    npm run dev
    ```
4.  Acesse a aplicação no seu navegador em `http://localhost:5173`.

---

## ✨ Funcionalidades Principais

* **Autenticação e Autorização Completas:** Sistema de registro e login seguro com **ASP.NET Core Identity** e **JWT**. Cada usuário só pode acessar seus próprios dados.
* **Análise via IA Generativa:** Utiliza a API do Google Gemini para interpretar comandos em linguagem natural e gerar planos de análise dinâmicos.
* **Persistência de Dados com Histórico Pessoal:** Salva os resultados de cada análise em um banco de dados PostgreSQL e permite que o usuário visualize seu histórico de forma segura.
* **Otimização para Arquivos Grandes:** Suporta `.csv` e `.xlsx` e utiliza técnicas de **streaming** para analisar arquivos com mais de 20.000 linhas com baixo consumo de memória.
* **Interface Reativa e Moderna:** Frontend construído com Vue 3, TypeScript e Pinia, com uma UI elegante e responsiva que se adapta ao estado de autenticação do usuário.

---

## 🗺️ Roadmap Futuro

Abaixo estão algumas funcionalidades e melhorias planejadas para futuras versões:

* **Login Social (OAuth 2.0):** Implementar a opção de "Login com Google" para facilitar o acesso de novos usuários.
* **Painel Administrativo:** Criar uma área restrita para usuários com a role "Admin", permitindo o gerenciamento de usuários.
* **Refinamento dos Gráficos:** Adicionar mais opções de customização e tipos de gráficos para o usuário.
* **Implantação e Acesso Público (Deployment):** Configurar pipelines de CI/CD para automatizar o deploy e hospedar a aplicação em uma plataforma de nuvem (Azure, AWS, etc.).

---

## 📜 Licença

Este projeto é de código aberto para fins educacionais e de portfólio, sob a licença MIT.