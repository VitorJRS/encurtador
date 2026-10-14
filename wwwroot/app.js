document.addEventListener("DOMContentLoaded", () => {
    // Referências DOM
    const shortenForm = document.getElementById("shortenForm");
    const urlInput = document.getElementById("urlInput");
    const btnPasteUrl = document.getElementById("btnPasteUrl");
    const btnClearUrl = document.getElementById("btnClearUrl");
    const customCodeInput = document.getElementById("customCodeInput");
    const domainPrefix = document.getElementById("domainPrefix");
    const livePreviewUrl = document.getElementById("livePreviewUrl");
    const btnSubmit = document.getElementById("btnSubmit");

    const resultCard = document.getElementById("resultCard");
    const resultShortUrl = document.getElementById("resultShortUrl");
    const resultOriginalUrl = document.getElementById("resultOriginalUrl");
    const btnCopyShortUrl = document.getElementById("btnCopyShortUrl");
    const btnOpenShortUrl = document.getElementById("btnOpenShortUrl");
    const btnNewLink = document.getElementById("btnNewLink");

    const resultQrImage = document.getElementById("resultQrImage");
    const btnDownloadQr = document.getElementById("btnDownloadQr");
    const btnCopyQrImage = document.getElementById("btnCopyQrImage");

    const linksTableBody = document.getElementById("linksTableBody");
    const searchFilter = document.getElementById("searchFilter");
    const linkCountBadge = document.getElementById("linkCountBadge");

    const qrModal = document.getElementById("qrModal");
    const modalQrTitle = document.getElementById("modalQrTitle");
    const modalQrUrl = document.getElementById("modalQrUrl");
    const modalQrImage = document.getElementById("modalQrImage");
    const modalDownloadBtn = document.getElementById("modalDownloadBtn");
    const btnCloseModal = document.getElementById("btnCloseModal");

    const toastContainer = document.getElementById("toastContainer");

    let allLinks = [];
    const currentHost = window.location.host || "localhost:5000";

    // Inicializa prefixo e prévia
    domainPrefix.textContent = `${currentHost}/`;
    updateLivePreview();

    // Carrega links salvos
    loadLinks();

    // 1. Atualização da prévia ao vivo enquanto digita o apelido/slug
    customCodeInput.addEventListener("input", () => {
        // Remove espaços ou caracteres proibidos automaticamente
        customCodeInput.value = customCodeInput.value.replace(/[^a-zA-Z0-9_-]/g, "");
        updateLivePreview();
    });

    function updateLivePreview() {
        const slug = customCodeInput.value.trim();
        if (slug) {
            livePreviewUrl.textContent = `${currentHost}/${slug}`;
            livePreviewUrl.style.color = "#38bdf8";
        } else {
            livePreviewUrl.textContent = `${currentHost}/[código-aleatório]`;
            livePreviewUrl.style.color = "#94a3b8";
        }
    }

    // 2. Botão de colar URL da área de transferência
    btnPasteUrl.addEventListener("click", async () => {
        try {
            const text = await navigator.clipboard.readText();
            if (text) {
                urlInput.value = text.trim();
                urlInput.dispatchEvent(new Event("input"));
                showToast("Link colado da área de transferência!", "success");
            }
        } catch {
            showToast("Permissão para colar negada pelo navegador.", "error");
        }
    });

    // 3. Botão limpar campo de URL
    urlInput.addEventListener("input", () => {
        if (urlInput.value.trim()) {
            btnClearUrl.classList.remove("hidden");
        } else {
            btnClearUrl.classList.add("hidden");
        }
    });

    btnClearUrl.addEventListener("click", () => {
        urlInput.value = "";
        btnClearUrl.classList.add("hidden");
        urlInput.focus();
    });

    // 4. Submeter formulário para encurtar
    shortenForm.addEventListener("submit", async (e) => {
        e.preventDefault();

        const url = urlInput.value.trim();
        const customCode = customCodeInput.value.trim();

        if (!url) {
            showToast("Por favor, informe a URL de destino.", "error");
            return;
        }

        btnSubmit.disabled = true;
        btnSubmit.innerHTML = `<span class="btn-submit-text">⏳ Encurtando e Gerando QR Code...</span>`;

        try {
            const res = await fetch("/api/shorten", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ url, customCode: customCode || null })
            });

            const data = await res.json();

            if (!res.ok) {
                throw new Error(data.error || "Ocorreu um erro ao processar o link.");
            }

            displayResult(data);
            showToast("🎉 Link encurtado e QR Code gerado com sucesso!", "success");

            // Recarrega a tabela de links
            loadLinks();
        } catch (err) {
            showToast(err.message, "error");
        } finally {
            btnSubmit.disabled = false;
            btnSubmit.innerHTML = `<span class="btn-submit-text">⚡ Encurtar Link e Gerar QR Code</span>`;
        }
    });

    // 5. Exibir card de resultado
    function displayResult(item) {
        resultShortUrl.value = item.shortUrl;
        resultOriginalUrl.textContent = item.originalUrl;
        btnOpenShortUrl.href = item.shortUrl;

        const qrEndpoint = `/api/qr/${encodeURIComponent(item.code)}?size=15&t=${Date.now()}`;
        resultQrImage.src = qrEndpoint;
        btnDownloadQr.href = qrEndpoint;
        btnDownloadQr.setAttribute("download", `qrcode-${item.code}.png`);

        resultCard.classList.remove("hidden");
        resultCard.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }

    // 6. Copiar Link Encurtado
    btnCopyShortUrl.addEventListener("click", () => {
        copyText(resultShortUrl.value, "Link copiado!");
    });

    // 7. Copiar Imagem do QR Code para a área de transferência
    btnCopyQrImage.addEventListener("click", async () => {
        try {
            const res = await fetch(resultQrImage.src);
            const blob = await res.blob();
            await navigator.clipboard.write([
                new ClipboardItem({ "image/png": blob })
            ]);
            showToast("Imagem do QR Code copiada! (Pronta para colar)", "success");
        } catch {
            showToast("Seu navegador não suporta copiar imagem diretamente. Use o botão Baixar PNG.", "error");
        }
    });

    // 8. Botão Criar Outro Link
    btnNewLink.addEventListener("click", () => {
        urlInput.value = "";
        customCodeInput.value = "";
        btnClearUrl.classList.add("hidden");
        updateLivePreview();
        urlInput.focus();
        window.scrollTo({ top: 0, behavior: "smooth" });
    });

    // 9. Carregar Lista de Links
    async function loadLinks() {
        try {
            const res = await fetch("/api/links");
            if (!res.ok) throw new Error("Erro ao carregar dados.");
            allLinks = await res.json();
            renderTable(allLinks);
        } catch (err) {
            linksTableBody.innerHTML = `<tr><td colspan="5" class="empty-state"><p>Erro ao conectar com o banco de dados.</p></td></tr>`;
        }
    }

    // 10. Renderizar tabela
    function renderTable(links) {
        linkCountBadge.textContent = `${links.length} link${links.length === 1 ? "" : "s"}`;

        if (!links || links.length === 0) {
            linksTableBody.innerHTML = `
                <tr>
                    <td colspan="5" class="empty-state">
                        <div class="empty-state-content">
                            <span class="empty-icon">📂</span>
                            <p>Nenhum link cadastrado no momento.</p>
                            <small>Encurte um link acima para salvar no seu banco permanente.</small>
                        </div>
                    </td>
                </tr>
            `;
            return;
        }

        linksTableBody.innerHTML = "";

        links.forEach(link => {
            const tr = document.createElement("tr");

            const formattedDate = new Date(link.createdAt).toLocaleDateString("pt-BR", {
                day: "2-digit",
                month: "2-digit",
                year: "numeric",
                hour: "2-digit",
                minute: "2-digit"
            });

            tr.innerHTML = `
                <td><span class="badge-slug">/${escapeHtml(link.code)}</span></td>
                <td><a href="${escapeHtml(link.shortUrl)}" target="_blank" class="table-short-link">${escapeHtml(link.shortUrl)}</a></td>
                <td><span class="table-orig-url" title="${escapeHtml(link.originalUrl)}">${escapeHtml(link.originalUrl)}</span></td>
                <td><span style="color: #94a3b8; font-size: 0.85rem;">${formattedDate}</span></td>
                <td class="text-right">
                    <div class="action-buttons">
                        <button type="button" class="btn-tbl-action btn-copy-row" title="Copiar Link">
                            📋 Copiar
                        </button>
                        <button type="button" class="btn-tbl-action btn-qr-row" title="Ver QR Code">
                            📱 QR
                        </button>
                        <button type="button" class="btn-tbl-action btn-tbl-del btn-del-row" title="Excluir link permanente">
                            🗑️
                        </button>
                    </div>
                </td>
            `;

            // Eventos dos botões da linha
            tr.querySelector(".btn-copy-row").addEventListener("click", () => {
                copyText(link.shortUrl, `Link /${link.code} copiado!`);
            });

            tr.querySelector(".btn-qr-row").addEventListener("click", () => {
                openModal(link.code, link.shortUrl);
            });

            tr.querySelector(".btn-del-row").addEventListener("click", () => {
                deleteLink(link.code);
            });

            linksTableBody.appendChild(tr);
        });
    }

    // 11. Filtro de Busca
    searchFilter.addEventListener("input", (e) => {
        const query = e.target.value.toLowerCase().trim();
        if (!query) {
            renderTable(allLinks);
            return;
        }

        const filtered = allLinks.filter(item =>
            item.code.toLowerCase().includes(query) ||
            item.originalUrl.toLowerCase().includes(query) ||
            item.shortUrl.toLowerCase().includes(query)
        );

        renderTable(filtered);
    });

    // 12. Modal do QR Code
    function openModal(code, shortUrl) {
        modalQrTitle.textContent = `QR Code: /${code}`;
        modalQrUrl.textContent = shortUrl;
        const qrUrl = `/api/qr/${encodeURIComponent(code)}?size=22`;
        modalQrImage.src = qrUrl;
        modalDownloadBtn.href = qrUrl;
        modalDownloadBtn.setAttribute("download", `qrcode-${code}.png`);
        qrModal.classList.remove("hidden");
    }

    btnCloseModal.addEventListener("click", () => {
        qrModal.classList.add("hidden");
    });

    qrModal.addEventListener("click", (e) => {
        if (e.target === qrModal) qrModal.classList.add("hidden");
    });

    // 13. Excluir Link
    async function deleteLink(code) {
        if (!confirm(`Tem certeza que deseja apagar o link permanente "/${code}"?`)) {
            return;
        }

        try {
            const res = await fetch(`/api/links/${encodeURIComponent(code)}`, {
                method: "DELETE"
            });

            if (!res.ok) {
                const data = await res.json();
                throw new Error(data.error || "Erro ao excluir.");
            }

            if (resultShortUrl.value.endsWith(`/${code}`)) {
                resultCard.classList.add("hidden");
            }

            showToast(`Link "/${code}" foi removido com sucesso.`, "success");
            loadLinks();
        } catch (err) {
            showToast(err.message, "error");
        }
    }

    // Utilitário de Cópia
    function copyText(text, successMessage) {
        navigator.clipboard.writeText(text).then(() => {
            showToast(successMessage, "success");
        }).catch(() => {
            prompt("Copie manualmente:", text);
        });
    }

    // Toast Notifications
    function showToast(message, type = "success") {
        const toast = document.createElement("div");
        toast.className = `toast toast-${type}`;
        const icon = type === "success" ? "✓" : "⚠️";
        toast.innerHTML = `<span>${icon}</span> <span>${escapeHtml(message)}</span>`;

        toastContainer.appendChild(toast);

        setTimeout(() => {
            if (toast.parentNode) {
                toast.parentNode.removeChild(toast);
            }
        }, 3000);
    }

    function escapeHtml(text) {
        const div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }
});
