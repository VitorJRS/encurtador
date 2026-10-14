# 🔗 Encurtador de Links & Gerador de QR Code (C# / .NET 8)

Um encurtador de links moderno, **100% gratuito**, **sem limites** e que **nunca expira**, desenvolvido em **C# (.NET 8 Minimal APIs)** com banco de dados local **SQLite** e gerador de **QR Code** integrado.

---

## ✨ Recursos

- **Links Permanentes**: Seus links são salvos no banco local SQLite (`links.db`) e nunca expiram (sem data de validade / sem TTL).
- **Nome Personalizado (Alias / Slug)**: Escolha o nome que preferir para o link encurtado (ex: `seu-site.com/promocao-zap` ou `meu-link`). Se preferir não escolher, o sistema gera automaticamente um código curto e seguro.
- **Gerador de QR Code Integrado**: Gera instantaneamente a imagem do QR Code em formato PNG de alta resolução para download ou compartilhamento.
- **Direto ao Ponto**: Sem necessidade de dashboards complexos, métricas pesadas ou monitoramento invasivo. Apenas encurte, copie o link e use o QR Code.
- **Redirecionamento Rápido**: Redirecionamento instantâneo via HTTP para o link original.
- **Interface Web Limpa e Responsiva**: Funciona tanto no desktop quanto em celulares, com busca em tempo real e cópia com 1 clique.

---

## 🚀 Como Executar

### Opção 1: Dois cliques no Windows (Mais Fácil)
Dê um duplo clique no arquivo:
```text
run.bat
```
Ele irá iniciar o servidor local e abrir automaticamente o seu navegador em `http://localhost:5000`.

---

### Opção 2: Pelo Terminal / Prompt de Comando

```bash
dotnet run
```
Ou especificando a porta:
```bash
dotnet run --urls "http://0.0.0.0:5000"
```

Abra no seu navegador: **[http://localhost:5000](http://localhost:5000)**

---

## 🛠️ Tecnologias Utilizadas

- **C# / .NET 8**: ASP.NET Core Minimal APIs de alta performance.
- **SQLite + Entity Framework Core**: Banco de dados embutido em arquivo único (`links.db`), sem necessidade de instalar servidores de banco externos (Postgres/MySQL) e 100% gratuito.
- **QRCoder**: Biblioteca nativa em C# para geração rápida e offline de QR Codes em PNG.
- **Frontend Autocontido**: HTML5, CSS3 moderno e Vanilla JavaScript (sem dependência de frameworks externos ou CDN).

---

## 🌐 Endpoints da API

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/` | Interface Web da aplicação |
| `POST` | `/api/shorten` | Cria um link encurtado (recebe `url` e `customCode` opcional) |
| `GET` | `/api/links` | Retorna todos os links cadastrados |
| `DELETE` | `/api/links/{code}` | Exclui um link cadastrado caso queira liberar o nome |
| `GET` | `/api/qr/{code}` | Retorna a imagem do QR Code em formato PNG |
| `GET` | `/{code}` | Redireciona o usuário para a URL original |

---

## ⚙️ Configurações (appsettings.json)

Caso vá hospedar a aplicação na nuvem ou em um domínio próprio (ex: `https://meu.link`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=links.db"
  },
  "BaseUrl": "https://meu.link"
}
```
*Se `BaseUrl` for deixado em branco, o sistema detecta automaticamente o domínio atual da requisição.*
