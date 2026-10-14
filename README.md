# 🔗 Encurtador de Links & Gerador de QR Code (C# / .NET 8)

Um encurtador de links moderno, **100% gratuito**, **sem limites** e que **nunca expira**, desenvolvido em **C# (.NET 8 Minimal APIs)**, otimizado para deploy imediato no **Render.com**.

---

## ✨ Recursos

- **100% na Nuvem**: Roda no Render.com gratuitamente e fica no ar 24h sem você precisar deixar nenhum computador ligado.
- **Banco de Dados Permanente**: Compatível com o **PostgreSQL Gratuito do Render** (seus links nunca são apagados quando o container reinicia ou hiberna) e com fallback automático para SQLite.
- **Nome Personalizado (Alias / Slug)**: Escolha o nome do link (ex: `seu-dominio.onrender.com/meu-zap`). Se deixar em branco, o sistema gera automaticamente um código exclusivo de 6 dígitos.
- **QR Code Studio Integrado**: Gera QR Code em alta definição com botões de **Baixar PNG** e **Copiar Imagem** direto para a área de transferência (para colar no WhatsApp, Word, etc.).
- **Prévia ao Vivo do Link**: Mostra exatamente como o link vai ficar enquanto você digita o nome personalizado.
- **Interface Moderna (Glassmorphism)**: Fundo escuro com luz ambiente, responsivo e com notificações flutuantes (Toasts).
- **Sem gráficos ou rastreamento**: Leve, rápido e direto ao ponto.

---

## 🚀 Como Publicar no Render.com (Passo a Passo)

### 1. Criar o Banco PostgreSQL no Render (100% Grátis)
Para que seus links fiquem salvos para sempre:
1. No painel do Render, clique em **New +** > **PostgreSQL**.
2. Dê um nome (ex: `encurtador-db`) e clique em **Create Database**.
3. Na página do banco criado, copie o valor do campo **Internal Database URL** (ou *External Database URL*).

---

### 2. Criar o Web Service
1. No painel do Render, clique em **New +** > **Web Service**.
2. Conecte o seu repositório do GitHub (`VitorJRS/encurtador`).
3. Preencha:
   - **Name**: `meu-encurtador` (ou o nome que quiser).
   - **Runtime**: `Docker` (já detectado automaticamente pelo `Dockerfile`).
   - **Instance Type**: `Free`.
4. Em **Environment Variables**, adicione:
   - **Key**: `DATABASE_URL`
   - **Value**: Cole a URL do banco que você copiou no passo 1.
5. Clique em **Deploy Web Service**!

Em cerca de 2 minutos seu encurtador estará no ar em:
`https://seu-nome.onrender.com`

---

## 💻 Como Rodar Localmente (Opcional)

Dê dois cliques no arquivo:
```text
run.bat
```
Ou execute no terminal:
```bash
dotnet run
```
Acesse em: `http://localhost:5000`
