CREATE TEXT INDEX word_by_name IF NOT EXISTS FOR (w:Word) ON (w.text)
CREATE TEXT INDEX edges_by_server IF NOT EXISTS FOR ()-[conn:FOLLOWED_BY]->() ON (conn.server)
CREATE TEXT INDEX server_by_id IF NOT EXISTS FOR (s:Server) ON (s.id)