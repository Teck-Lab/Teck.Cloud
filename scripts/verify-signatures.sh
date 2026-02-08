#!/usr/bin/env bash
#
# Verify Cosign signatures for Teck Cloud images and artifacts
# 
# Usage:
#   ./scripts/verify-signatures.sh <version> [service]
#
# Examples:
#   ./scripts/verify-signatures.sh 1.0.0          # Verify all services
#   ./scripts/verify-signatures.sh 1.0.0 catalog  # Verify only catalog service
#

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
HARBOR_URL="${HARBOR_URL:-harbor.tecklab.dk}"
GITHUB_REPO="${GITHUB_REPO:-Teck/Teck.Cloud}"
CERT_IDENTITY_REGEX="^https://github.com/${GITHUB_REPO}/.*"
OIDC_ISSUER="https://token.actions.githubusercontent.com"

# Functions
print_header() {
    echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_info() {
    echo -e "${YELLOW}ℹ${NC} $1"
}

check_dependencies() {
    print_header "Checking Dependencies"
    
    local missing_deps=()
    
    if ! command -v cosign &> /dev/null; then
        missing_deps+=("cosign")
    fi
    
    if ! command -v gh &> /dev/null; then
        missing_deps+=("gh")
    fi
    
    if ! command -v jq &> /dev/null; then
        missing_deps+=("jq")
    fi
    
    if [ ${#missing_deps[@]} -ne 0 ]; then
        print_error "Missing required dependencies: ${missing_deps[*]}"
        echo ""
        echo "Install instructions:"
        echo "  - cosign: https://docs.sigstore.dev/cosign/installation/"
        echo "  - gh: https://cli.github.com/manual/installation"
        echo "  - jq: https://stedolan.github.io/jq/download/"
        exit 1
    fi
    
    print_success "All dependencies installed"
    echo "  - cosign: $(cosign version | head -n1)"
    echo "  - gh: $(gh --version | head -n1)"
    echo "  - jq: $(jq --version)"
}

verify_image_signature() {
    local service=$1
    local version=$2
    local image_ref="${HARBOR_URL}/teck-lab/${service}:${version}"
    
    print_header "Verifying Image Signature: ${service}"
    
    print_info "Image: ${image_ref}"
    
    if cosign verify \
        --certificate-identity-regexp="${CERT_IDENTITY_REGEX}" \
        --certificate-oidc-issuer="${OIDC_ISSUER}" \
        "${image_ref}" > /dev/null 2>&1; then
        print_success "Image signature verified successfully"
        
        # Show signature details
        echo ""
        echo "Signature details:"
        cosign verify \
            --certificate-identity-regexp="${CERT_IDENTITY_REGEX}" \
            --certificate-oidc-issuer="${OIDC_ISSUER}" \
            "${image_ref}" 2>/dev/null | jq -r '.[0] | "  Subject: \(.optional.Subject // "N/A")\n  Issuer: \(.optional.Issuer // "N/A")\n  Integrated Time: \(.optional.SignedCertificateTimestamp // [] | .[0].SignedCertificateTimestamp // "N/A")"'
        return 0
    else
        print_error "Image signature verification failed"
        return 1
    fi
}

verify_sbom_signature() {
    local service=$1
    local version=$2
    local sbom_format=$3  # spdx or cyclonedx
    
    print_header "Verifying ${sbom_format^^} SBOM Signature: ${service}"
    
    local sbom_file="sbom-${service}.${sbom_format}.json"
    local sig_file="${sbom_file}.sig"
    local cert_file="${sbom_file}.cert"
    local bundle_file="${sbom_file}.bundle"
    
    # Create temp directory
    local temp_dir=$(mktemp -d)
    trap "rm -rf ${temp_dir}" EXIT
    
    cd "${temp_dir}"
    
    print_info "Downloading SBOM artifacts from GitHub release v${version}"
    
    # Download SBOM files
    if gh release download "v${version}" \
        --repo "${GITHUB_REPO}" \
        --pattern "${sbom_file}*" 2>/dev/null; then
        
        print_success "SBOM artifacts downloaded"
        
        # Verify using bundle if available
        if [ -f "${bundle_file}" ]; then
            if cosign verify-blob \
                --bundle "${bundle_file}" \
                --certificate-identity-regexp="${CERT_IDENTITY_REGEX}" \
                --certificate-oidc-issuer="${OIDC_ISSUER}" \
                "${sbom_file}" > /dev/null 2>&1; then
                print_success "${sbom_format^^} SBOM signature verified successfully"
                
                # Show SBOM stats
                echo ""
                echo "SBOM statistics:"
                if [ "${sbom_format}" = "spdx" ]; then
                    echo "  SPDX Version: $(jq -r '.spdxVersion' ${sbom_file})"
                    echo "  Packages: $(jq '.packages | length' ${sbom_file})"
                elif [ "${sbom_format}" = "cyclonedx" ]; then
                    echo "  CycloneDX Version: $(jq -r '.specVersion' ${sbom_file})"
                    echo "  Components: $(jq '.components | length' ${sbom_file})"
                fi
                return 0
            else
                print_error "${sbom_format^^} SBOM signature verification failed"
                return 1
            fi
        else
            print_error "Bundle file not found: ${bundle_file}"
            return 1
        fi
    else
        print_error "Failed to download SBOM artifacts"
        return 1
    fi
}

verify_vex_signature() {
    local service=$1
    local version=$2
    
    print_header "Verifying VEX Signature: ${service}"
    
    local vex_file="vex-${service}.openvex.json"
    local bundle_file="${vex_file}.bundle"
    
    # Create temp directory
    local temp_dir=$(mktemp -d)
    trap "rm -rf ${temp_dir}" EXIT
    
    cd "${temp_dir}"
    
    print_info "Downloading VEX artifacts from GitHub release v${version}"
    
    # Download VEX files
    if gh release download "v${version}" \
        --repo "${GITHUB_REPO}" \
        --pattern "${vex_file}*" 2>/dev/null; then
        
        print_success "VEX artifacts downloaded"
        
        if [ -f "${bundle_file}" ]; then
            if cosign verify-blob \
                --bundle "${bundle_file}" \
                --certificate-identity-regexp="${CERT_IDENTITY_REGEX}" \
                --certificate-oidc-issuer="${OIDC_ISSUER}" \
                "${vex_file}" > /dev/null 2>&1; then
                print_success "VEX signature verified successfully"
                
                # Show VEX stats
                echo ""
                echo "VEX statistics:"
                echo "  Statements: $(jq '.statements | length' ${vex_file})"
                echo "  Affected: $(jq '[.statements[] | select(.status == "affected")] | length' ${vex_file})"
                echo "  Not Affected: $(jq '[.statements[] | select(.status == "not_affected")] | length' ${vex_file})"
                echo "  Under Investigation: $(jq '[.statements[] | select(.status == "under_investigation")] | length' ${vex_file})"
                return 0
            else
                print_error "VEX signature verification failed"
                return 1
            fi
        else
            print_error "Bundle file not found: ${bundle_file}"
            return 1
        fi
    else
        print_error "Failed to download VEX artifacts (may not exist for this service)"
        return 1
    fi
}

verify_attestations() {
    local service=$1
    local version=$2
    local image_ref="${HARBOR_URL}/teck-lab/${service}:${version}"
    
    print_header "Verifying Attestations: ${service}"
    
    print_info "Verifying SBOM attestation"
    if gh attestation verify "oci://${image_ref}" --owner "$(echo ${GITHUB_REPO} | cut -d'/' -f1)" > /dev/null 2>&1; then
        print_success "SBOM attestation verified"
    else
        print_error "SBOM attestation verification failed"
        return 1
    fi
    
    print_info "Verifying build provenance attestation (SLSA)"
    if gh attestation verify "oci://${image_ref}" \
        --predicate-type "https://slsa.dev/provenance/v1" \
        --owner "$(echo ${GITHUB_REPO} | cut -d'/' -f1)" > /dev/null 2>&1; then
        print_success "Build provenance attestation verified (SLSA L3)"
    else
        print_error "Build provenance attestation verification failed"
        return 1
    fi
    
    return 0
}

verify_service() {
    local service=$1
    local version=$2
    
    print_header "🔐 Verifying Service: ${service} v${version}"
    
    local failed=0
    
    # Verify image signature
    verify_image_signature "${service}" "${version}" || failed=1
    
    # Verify SPDX SBOM signature
    verify_sbom_signature "${service}" "${version}" "spdx" || failed=1
    
    # Verify CycloneDX SBOM signature
    verify_sbom_signature "${service}" "${version}" "cyclonedx" || failed=1
    
    # Verify VEX signature
    verify_vex_signature "${service}" "${version}" || failed=1
    
    # Verify attestations
    verify_attestations "${service}" "${version}" || failed=1
    
    if [ $failed -eq 0 ]; then
        print_header "✅ ALL VERIFICATIONS PASSED for ${service}"
        return 0
    else
        print_header "❌ SOME VERIFICATIONS FAILED for ${service}"
        return 1
    fi
}

main() {
    if [ $# -lt 1 ]; then
        echo "Usage: $0 <version> [service]"
        echo ""
        echo "Examples:"
        echo "  $0 1.0.0          # Verify all services"
        echo "  $0 1.0.0 catalog  # Verify only catalog service"
        exit 1
    fi
    
    local version=$1
    local specific_service=${2:-}
    
    # Remove 'v' prefix if present
    version="${version#v}"
    
    check_dependencies
    
    if [ -n "${specific_service}" ]; then
        # Verify specific service
        verify_service "${specific_service}" "${version}"
        exit $?
    else
        # Verify all services
        print_header "🔍 Discovering services from release v${version}"
        
        # Get list of SBOM files from release
        local services=()
        while IFS= read -r asset; do
            if [[ $asset =~ sbom-([^.]+)\.spdx\.json ]]; then
                services+=("${BASH_REMATCH[1]}")
            fi
        done < <(gh release view "v${version}" --repo "${GITHUB_REPO}" --json assets -q '.assets[].name' 2>/dev/null)
        
        if [ ${#services[@]} -eq 0 ]; then
            print_error "No services found in release v${version}"
            echo ""
            echo "Available releases:"
            gh release list --repo "${GITHUB_REPO}" --limit 5
            exit 1
        fi
        
        print_success "Found ${#services[@]} service(s): ${services[*]}"
        
        local total_failed=0
        for service in "${services[@]}"; do
            verify_service "${service}" "${version}" || total_failed=$((total_failed + 1))
        done
        
        echo ""
        print_header "📊 Verification Summary"
        echo "  Total services: ${#services[@]}"
        echo "  Passed: $((${#services[@]} - total_failed))"
        echo "  Failed: ${total_failed}"
        
        if [ $total_failed -eq 0 ]; then
            echo ""
            print_success "All verifications passed! 🎉"
            exit 0
        else
            echo ""
            print_error "${total_failed} service(s) failed verification"
            exit 1
        fi
    fi
}

main "$@"
