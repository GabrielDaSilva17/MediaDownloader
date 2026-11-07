# MediaDownloader 🎬🎵

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Plataforma](https://img.shields.io/badge/Plataforma-Windows%20x64-0078D6?logo=windows)](https://microsoft.com)
[![yt-dlp](https://img.shields.io/badge/Powered%20By-yt--dlp-red)](https://github.com/yt-dlp/yt-dlp)
[![FFmpeg](https://img.shields.io/badge/Powered%20By-FFmpeg-green?logo=ffmpeg)](https://ffmpeg.org/)
[![License](https://img.shields.io/badge/Licen%C3%A7a-MIT-blue)](LICENSE)

Interface moderna, rápida e minimalista para download e conversão de vídeos, áudios e playlists completas com alta qualidade no Windows, impulsionada pelo **yt-dlp** e **FFmpeg**.

Desenvolvido por [**GabrielDaSilva17**](https://github.com/GabrielDaSilva17).

---

## ✨ Principais Funcionalidades

- ⚡ **Playlists Otimizadas:** Varredura quase instantânea de playlists inteiras (`--flat-playlist`), ignorando vídeos privados/excluídos automaticamente sem travar o download.
- 📁 **Organização Automática:** Downloads de playlists são salvos em pastas dedicadas com o nome da lista e arquivos numerados em ordem (`01 - Título`, `02 - Título`).
- 🎨 **Interface Dark Monocromática:** Visual limpo, em preto e branco de alto contraste, sem poluição visual e com tamanho de janela fixo.
- 🔒 **Instalação Automática & Criptografada:** Na primeira execução, o app baixa o `yt-dlp.exe` e `ffmpeg.exe` via URLs oficiais criptografadas em AES-256 a velocidades de CDN (20~50 MB/s).
- 🔄 **Auto-Atualizador Integrado:** 
  - Atualização do próprio aplicativo via GitHub Releases em 1 clique.
  - Atualização dos binários do `yt-dlp` diretamente no menu de ferramentas (`yt-dlp -U`).
- 🎧 **Multi-Formatos:**
  - **Vídeo:** MP4, MKV, WEBM (até 4K / 2160p / 60fps).
  - **Áudio:** MP3, M4A, WAV, FLAC, OPUS (até 320 kbps com extração de thumbnail).

---

## 📥 Como Baixar e Usar

1. Acesse a aba [**Releases**](https://github.com/GabrielDaSilva17/MediaDownloader/releases/latest).
2. Baixe o pacote de sua preferência:
   - **`MediaDownloader_Completo.zip`** *(Recomendado)*: Já vem pronto com todos os executáveis offline (`yt-dlp` e `FFmpeg`), basta extrair e abrir.
   - **`MediaDownloader_Leve.zip`**: Mais compacto (~60 MB), baixa os componentes na primeira abertura.
3. Extraia o arquivo ZIP para uma pasta de sua escolha.
4. Execute **`MediaDownloader.exe`**.

> [!NOTE]  
> **Aviso de "Editor Desconhecido" no Windows (SmartScreen)?**  
> Como o aplicativo é um projeto independente sem certificado comercial pago da Microsoft, o Windows pode exibir uma tela azul informativa.  
> Para abrir: clique em **"Mais informações"** e depois em **"Executar assim mesmo"**.

---

## 🛠️ Tecnologias Utilizadas

- **C# / .NET 10** (WPF - Windows Presentation Foundation)
- **yt-dlp** (Backend robusto para extração de mídias)
- **FFmpeg & FFprobe** (Conversão de codecs de áudio/vídeo)
- **AES-256 Cryptography** (Proteção e integridade de fontes oficiais)
- **GitHub REST API** (Verificação de novos releases em tempo real)

---

## 💻 Compilação a partir do Código-Fonte

Pré-requisitos:
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) instalado no Windows.

```bash
# 1. Clonar o repositório
git clone https://github.com/GabrielDaSilva17/MediaDownloader.git
cd MediaDownloader

# 2. Restaurar e compilar
dotnet build -c Release

# 3. Gerar executável independente (Single-File / Self-Contained)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
```

---

## 📄 Licença

Distribuído sob a licença MIT. Consulte o arquivo `LICENSE` para mais informações.

