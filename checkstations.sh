#!/usr/bin/env bash

set -euo pipefail
cd "$(dirname "$0")"

SRC="${1:-radiostations.txt}"
OUT="${2:-radiostations.checked.txt}"
JOBS="${JOBS:-8}"
UA="Mozilla/5.0 (X11; Linux x86_64)"
ACCEPT="audio/mpeg,*/*"
TMPD="$(mktemp -d "$(pwd)/.stations-check.XXXXXX")"
trap 'rm -rf "$TMPD"' EXIT

[ -f "$SRC" ] || { echo "ERROR: $SRC not found"; exit 1; }
awk -F'|' 'NF>=4 { printf "%s\0", $0 }' "$SRC" > "$TMPD/worklist"
TOTAL=$(tr -cd '\0' < "$TMPD/worklist" | wc -c)
[ "$TOTAL" -gt 0 ] || { echo "ERROR: $SRC empty"; exit 1; }

check_one() {
  local line="$1" p="$BASHPID" url got code ct
  url="${line%%|*}"
  got=$(curl -s -m 12 -L --range 0-131071 \
        -A "$UA" -H "Accept: $ACCEPT" \
        -o "$TMPD/b.$p" -w "%{http_code} %{content_type}" "$url" 2>/dev/null || true)
  code="${got%% *}"
  ct="${got#* }"
  if [ "$code" = "200" ] || [ "$code" = "206" ]; then
    if LC_ALL=C grep -aqE $'\xff[\xf0-\xff]' "$TMPD/b.$p" 2>/dev/null \
       || [[ "$ct" == audio/mpeg* ]] || [[ "$ct" == audio/aac* ]]; then
      echo "$line" > "$TMPD/r.$p"
      printf 'OK      %s  [%s]\n' "$url" "$ct" > "$TMPD/s.$p"
    else
      printf 'NOAUDIO %s  [%s]\n' "$url" "$ct" > "$TMPD/s.$p"
    fi
  else
    printf '%-8s %s      [%s]\n' "$code" "$url" "$ct" > "$TMPD/s.$p"
  fi
}
export -f check_one
export TMPD UA ACCEPT

echo "checking $TOTAL stations (JOBS=$JOBS)..."
xargs -0 -n 1 -P "$JOBS" bash -c 'check_one "$1"' _ < "$TMPD/worklist"

cat "$TMPD"/r.* 2>/dev/null | sort -u > "$OUT"
cat "$TMPD"/s.* 2>/dev/null | sort -u > "${OUT%.txt}.skipped.txt"
OKCNT=$(wc -l < "$OUT")

echo ""
echo "checked:  $TOTAL"
echo "working:  $OKCNT  ->  $OUT"
echo "dropped:  $((TOTAL-OKCNT))  ->  ${OUT%.txt}.skipped.txt"
echo ""
echo "Install:  cp \"$OUT\"   to   <game>/BepInEx/plugins/radiostations.txt"
if [ "$OKCNT" -eq 0 ]; then
  echo "WARNING: 0 stations survived. See ${OUT%.txt}.skipped.txt for reasons."
fi