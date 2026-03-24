import Quality from 'Quality/Quality';
import { QualityProfileFormatItem } from './CustomFormat';

export interface QualityProfileQualityItem {
  id?: number;
  quality?: Quality;
  items: QualityProfileQualityItem[];
  allowed: boolean;
  name?: string;
}

interface QualityProfile {
  id: number;
  name: string;
  upgradeAllowed?: boolean;
  cutoff?: number;
  items?: QualityProfileQualityItem[];
  minFormatScore?: number;
  cutoffFormatScore?: number;
  minUpgradeFormatScore?: number;
  formatItems?: QualityProfileFormatItem[];
  // YouTube tokenized quality profile fields (from API)
  enabled?: boolean;
  maxHeight?: number | null;
  minHeight?: number | null;
  minFps?: number | null;
  maxFps?: number | null;
  allowHdr?: boolean;
  allowSdr?: boolean;
  allowedVideoCodecs?: string[];
  preferredVideoCodecs?: string[];
  allowedAudioCodecs?: string[];
  preferredAudioCodecs?: string[];
  allowedContainers?: string[];
  preferredContainers?: string[];
  preferSeparateStreams?: boolean;
  allowMuxedFallback?: boolean;
  fallbackMode?: number;
  failIfBelowMinHeight?: boolean;
  retryForBetterFormats?: boolean;
  retryWindowMinutes?: number | null;
}

export default QualityProfile;
