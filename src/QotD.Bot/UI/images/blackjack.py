import os

suits = {
    'Hearts': ('♥', '#D32F2F'),
    'Diamonds': ('♦', '#D32F2F'),
    'Spades': ('♠', '#212121'),
    'Clubs': ('♣', '#212121')
}

ranks = ['A', '2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K']

svg_template = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 180" width="120" height="180">
  <rect x="1" y="1" width="118" height="178" rx="10" ry="10" fill="white" stroke="#333" stroke-width="2"/>
  
  <text x="12" y="30" font-family="Arial, sans-serif" font-weight="bold" font-size="24" fill="{color}">{rank}</text>
  <text x="12" y="55" font-family="Arial, sans-serif" font-size="24" fill="{color}">{symbol}</text>
  
  <text x="60" y="110" font-family="Arial, sans-serif" font-size="60" text-anchor="middle" fill="{color}">{symbol}</text>
  
  <g transform="translate(120, 180) rotate(180)">
    <text x="12" y="30" font-family="Arial, sans-serif" font-weight="bold" font-size="24" fill="{color}">{rank}</text>
    <text x="12" y="55" font-family="Arial, sans-serif" font-size="24" fill="{color}">{symbol}</text>
  </g>
</svg>"""

os.makedirs("blackjack_deck", exist_ok=True)

for suit_name, (symbol, color) in suits.items():
    for rank in ranks:
        svg_content = svg_template.format(rank=rank, symbol=symbol, color=color)
        filename = f"blackjack_deck/{rank}_of_{suit_name}.svg"
        with open(filename, "w", encoding="utf-8") as f:
            f.write(svg_content)

print("Alle 52 Karten wurden im Ordner 'blackjack_deck' gespeichert!")