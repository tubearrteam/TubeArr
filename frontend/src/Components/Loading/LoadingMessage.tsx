import React from 'react';
import styles from './LoadingMessage.css';

const messages = [
  'Buffering... but like, emotionally',
  'Finding your next rabbit hole...',
  'Previously on YouTubeArr...',
  'Negotiating with the algorithm...',
  'Summoning the bitrate gods...',
  'Spinning up the recommendation engine...',
  'Checking if this video is actually worth it...',
  'Loading 3 ads you can’t skip...',
  'Scanning for “skip intro” moments...',
  'Downloading... definitely not piracy...',
  'Calibrating autoplay regret...',
  'Reading 10,000 comments for no reason...',
  'This video will change your life (it won’t)',
  'Optimizing clickbait levels...',
  'Converting curiosity into wasted hours...',
  'Finding that one video you saw 6 years ago...',
  'Reconstructing your questionable watch history...',
  'Resolving “why is this in my feed?”...',
  'Generating thumbnails with unnecessary red circles...',
  'Just one more video...',
];

let message: string | null = null;

function LoadingMessage() {
  if (!message) {
    const index = Math.floor(Math.random() * messages.length);
    message = messages[index];
  }

  return <div className={styles.loadingMessage}>{message}</div>;
}

export default LoadingMessage;
