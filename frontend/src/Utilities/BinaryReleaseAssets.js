/**
 * Filter GitHub release assets for the TubeArr host (where binaries run), not the browser.
 * OS/arch come from system status (RuntimeInformation on the server).
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

/**
 * @param {object} [systemStatus] state.system.status.item from the API
 * @returns {{ os: string, arch: string, label: string }}
 */
export function buildHostBinaryPlatform(systemStatus) {
  const s = systemStatus || {};
  let os = s.hostBinaryPlatformOs;
  if (os !== 'windows' && os !== 'darwin' && os !== 'linux') {
    if (s.isWindows) {
      os = 'windows';
    } else if (s.isOsx) {
      os = 'darwin';
    } else {
      os = 'linux';
    }
  }

  let arch = s.hostBinaryPlatformArch;
  if (arch !== 'arm64' && arch !== 'arm' && arch !== 'x64') {
    arch = 'x64';
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

/** @param {{ os: string, arch: string }} platform */
export function filterYtDlpAssets(assets, platform) {
  const { os, arch } = platform;
  const list = Array.isArray(assets) ? assets : [];
  return list.filter((a) => matchesYtDlpAsset(a.name, os, arch));
}

/** @param {{ os: string, arch: string }} platform */
export function filterFfmpegAssets(assets, platform) {
  const { os, arch } = platform;
  const list = Array.isArray(assets) ? assets : [];
  return list.filter((a) => matchesFfmpegAsset(a.name, os, arch));
}
