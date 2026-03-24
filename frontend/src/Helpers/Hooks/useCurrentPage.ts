import { useNavigationType } from 'react-router-dom';

function useCurrentPage() {
  return useNavigationType() === 'POP';
}

export default useCurrentPage;
