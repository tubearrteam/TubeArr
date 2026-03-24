import React from 'react';
import Label from 'Components/Label';
import { kinds } from 'Helpers/Props';
import CustomFormat from 'typings/CustomFormat';

interface VideoFormatsProps {
  formats: CustomFormat[];
}

function VideoFormats({ formats }: VideoFormatsProps) {
  return (
    <div>
      {formats.map(({ id, name }) => (
        <Label key={id} kind={kinds.INFO}>
          {name}
        </Label>
      ))}
    </div>
  );
}

export default VideoFormats;
