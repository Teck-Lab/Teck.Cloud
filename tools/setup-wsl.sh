#!/usr/bin/env bash
# tools/setup-wsl.sh
# One-time WSL2 developer environment setup for this workspace.
# Run from any directory: bash /mnt/c/Users/<you>/Documents/GitHub/Infrastructure/Teck.Cloud/tools/setup-wsl.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "==> Teck.Cloud WSL2 setup"
echo "    Repo root: $REPO_ROOT"
echo ""

# ── 1. System dependencies ────────────────────────────────────────────────────
echo "[1/5] Installing system dependencies..."
sudo apt-get update -qq
sudo apt-get install -y -qq unzip curl
echo "      OK"

# ── 2. Bun ───────────────────────────────────────────────────────────────────
echo "[2/5] Installing bun..."
if command -v bun &>/dev/null; then
  echo "      Already installed: $(bun --version)"
else
  curl -fsSL https://bun.sh/install | bash
  export PATH="$HOME/.bun/bin:$PATH"
  echo "      Installed: $(bun --version)"
fi

# ── 3. opencode symlink (makes it available in all shell types) ───────────────
echo "[3/5] Symlinking opencode to /usr/local/bin..."
if [[ -f "$HOME/.opencode/bin/opencode" ]]; then
  sudo ln -sf "$HOME/.opencode/bin/opencode" /usr/local/bin/opencode
  echo "      OK: $(opencode --version)"
else
  echo "      SKIP: opencode binary not found at ~/.opencode/bin/opencode"
  echo "      Run: bunx oh-my-openagent install --no-tui --claude=no --gemini=no --openai=no --copilot=yes --opencode-go=yes"
fi

# ── 4. oh-my-openagent skills symlinks ───────────────────────────────────────
echo "[4/5] Symlinking .github/skills into ~/.config/opencode/skills..."
SKILLS_SRC="$REPO_ROOT/.github/skills"
SKILLS_DST="$HOME/.config/opencode/skills"
mkdir -p "$SKILLS_DST"

if [[ -d "$SKILLS_SRC" ]]; then
  linked=0
  for skill_dir in "$SKILLS_SRC"/*/; do
    skill_name="$(basename "$skill_dir")"
    target="$SKILLS_DST/$skill_name"
    if [[ -L "$target" ]]; then
      echo "      Already linked: $skill_name"
    elif [[ -e "$target" ]]; then
      echo "      SKIP (exists, not a symlink): $skill_name"
    else
      ln -s "$skill_dir" "$target"
      echo "      Linked: $skill_name"
      ((linked++))
    fi
  done
  echo "      Done"
else
  echo "      SKIP: $SKILLS_SRC not found"
fi

# ── 5. Summary ────────────────────────────────────────────────────────────────
echo ""
echo "[5/5] Summary"
echo "      bun:      $(command -v bun &>/dev/null && bun --version || echo 'not found')"
echo "      opencode: $(command -v opencode &>/dev/null && opencode --version || echo 'not found')"
echo "      skills:   $(ls "$SKILLS_DST" 2>/dev/null | tr '\n' ' ' || echo 'none')"
echo ""
echo "Next steps (if not done):"
echo "  1. Install oh-my-openagent:"
echo "     bunx oh-my-openagent install --no-tui --claude=no --gemini=no --openai=no --copilot=yes --opencode-go=yes"
echo "  2. Auth GitHub Copilot: opencode auth login"
echo "  3. Auth opencode Go:    opencode auth login  (select OpenCode Go, paste API key)"
echo "  4. Start:               cd /mnt/c/Users/<you>/Documents/GitHub/Infrastructure && opencode"
