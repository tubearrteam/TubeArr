/**
 * Client-side filtering of GitHub release assets to likely OS/arch matches.
 * Used by Tools → yt-dlp and Tools → FFmpeg download pickers.
 */

function isNoiseAssetName(name) {
  const n = (name || '').toLowerCase();
  return (
    n.includes('sha256') ||
    n.includes('sha2-') ||
    n.includes('checksum') ||
    n.endsWith('.sig') ||
    n.includes('512sums') ||
    n === '_update_spec'
  );
}

export function getClientBinaryPlatform() {
  if (typeof navigator === 'undefined') {
    return { os: 'linux', arch: 'x64', label: 'Linux · x64' };
  }

  const ua = (navigator.userAgent || '').toLowerCase();
  const platform = (navigator.platform || '').toLowerCase();

  let os = 'linux';
  if (/win/.test(platform) || ua.includes('windows')) {
    os = 'windows';
  } else if (/mac|iphone|ipad|ipod/.test(platform) || ua.includes('mac os')) {
    os = 'darwin';
  }

  let arch = 'x64';
  if (ua.includes('aarch64') || ua.includes('arm64')) {
    arch = 'arm64';
  } else if (/\barm\b/.test(ua) || ua.includes('armv7')) {
    arch = 'arm';
  }

  const osLabel = os === 'windows' ? 'Windows' : os === 'darwin' ? 'macOS' : 'Linux';
  const archLabel = arch === 'arm64' ? 'ARM64' : arch === 'arm' ? 'ARMv7' : 'x64';
  return { os, arch, label: `${osLabel} · ${archLabel}` };
}

function matchesYtDlpAsset(name, os, arch) {
  const n = (name || '').toLowerCase();
  if (isNoiseAssetName(name)) {
    return false;
  }

  if (os === 'windows') {
    if (n.includes('linux') || n.includes('macos') || n.includes('musl')) {
      return false;
    }
    if (arch === 'arm64') {
      return n.includes('arm64.exe') || n.includes('win_arm64');
    }
    if (arch === 'arm') {
      return false;
    }
    if (n.includes('arm64') || n.includes('aarch64') || n.includes('win_arm64')) {
      return false;
    }
    // x64 Windows runs 32-bit (x86) builds too (WoW64).
    return n.endsWith('.exe') || (n.includes('win') && n.includes('.zip'));
  }

  if (os === 'darwin') {
    return n.includes('macos');
  }

  if (os === 'linux') {
    if (n.includes('win') || n.endsWith('.exe') || n.includes('macos')) {
      return false;
    }
    if (arch === 'arm64') {
      return n.includes('aarch64') || (n.includes('arm64') && !n.includes('armv7'));
    }
    if (arch === 'arm') {
      return n.includes('armv7');
    }
    if (n.includes('aarch64') || n.includes('armv7')) {
      return false;
    }
    return n.includes('linux') || n === 'yt-dlp';
  }

  return false;
}

function matchesFfmpegAsset(name, os, arch) {
  const n = (name || '').toLowerCase();
  if (n.includes('checksum')) {
    return false;
  }

  if (os === 'windows') {
    if (!n.includes('win')) {
      return false;
    }
    if (arch === 'arm64') {
      return n.includes('winarm64');
    }
    // x64 Windows runs 32-bit (win32) FFmpeg builds as well.
    return (n.includes('win64') || n.includes('win32')) && !n.includes('winarm64');
  }

  if (os === 'linux') {
    if (!n.includes('linux')) {
      return false;
    }
    if (arch === 'arm64') {
      return n.includes('linuxarm64');
    }
    return n.includes('linux64') && !n.includes('linuxarm64');
  }

  if (os === 'darwin') {
    return n.includes('macos') || n.includes('darwin') || n.includes('osx');
  }

  return false;
}

export function filterYtDlpAssets(assets) {
  const { os, arch } = getClientBinaryPlatform();
  const list = Array.isArray(assets) ? assets : [];
  return list.filter((a) => matchesYtDlpAsset(a.name, os, arch));
}

export function filterFfmpegAssets(assets) {
  const { os, arch } = getClientBinaryPlatform();
  const list = Array.isArray(assets) ? assets : [];
  return list.filter((a) => matchesFfmpegAsset(a.name, os, arch));
}
