# Art/

A arte do jogo. O jogo acha cada imagem **pelo nome do arquivo** — não há lista para registrar.

```
Art/Ingredients/<id>.png     128×128  ex.: bacon.png, ovo.png        (id = campo Id do .tres)
Art/Vessels/<id>.png         128×128  frigideira.png, panela.png, tigela.png
Art/Dishes/<receita>.png     256×256  omelete-de-queijo.png          (= nome do .tres em Content/Anchors/)
Art/Dishes/generico-<id>.png 256×256  generico-panela.png            (prato improvisado naquele recipiente)
Art/Vendors/<id>.png         256×256  mercado.png, conveniencia.png
Art/House/<id>.png           360×360  geladeira.png, fogao.png, pia.png, porta.png
Art/House/fundo-cozinha.png  1920×1080 (opaco)
Art/Hud/<id>.png             64×64    fome.png, sede.png, humor.png, pausa.png, velocidade-1..3.png
Art/Hud/retrato-sim.png      128×128
Art/Menu/logo.png            760×320
Art/Menu/fundo.png           1920×1080 (opaco)
```

PNG com fundo transparente, nome em minúsculas, sem acento nem espaço. Depois de salvar, abra o
editor do Godot uma vez para ele importar o arquivo. Lista completa, com o status de cada imagem:
`docs/arte-e-fluxo.xlsx`. Explicação: `docs/ARTE-E-FLUXO.md`.
