# Harbor Registry Configuration

## GitHub Secrets Required

### HARBOR_URL
**Value:** `harbor.tecklab.dk` (**NO** `https://` prefix!)

**❌ Wrong:** `https://harbor.tecklab.dk`  
**✅ Correct:** `harbor.tecklab.dk`

### HARBOR_USERNAME & HARBOR_PASSWORD
Your Harbor credentials or robot account token.

---

## Fix "blob upload invalid" Error

**Cause:** `HARBOR_URL` secret includes `https://` prefix

**Fix:**
1. Go to: Repository → Settings → Secrets and variables → Actions
2. Edit: `HARBOR_URL`
3. Remove `https://` prefix
4. Save as: `harbor.tecklab.dk`

---

## Harbor Web UI vs Registry

- **Web UI:** `https://harbor.tecklab.dk` (for browsing)
- **Registry:** `harbor.tecklab.dk` (for docker push/pull)
