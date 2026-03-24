import Video from 'Video/Video';
import { update } from 'Store/Actions/baseActions';

function updateVideos(
  section: string,
  videos: Video[],
  videoIds: number[],
  options: Partial<Video>
) {
  const data = videos.reduce<Video[]>((result, item) => {
    if (videoIds.indexOf(item.id) > -1) {
      result.push({
        ...item,
        ...options,
      });
    } else {
      result.push(item);
    }

    return result;
  }, []);

  return update({ section, data });
}

export default updateVideos;
