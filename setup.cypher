CREATE TEXT INDEX word_by_name IF NOT EXISTS FOR (w:Word) ON (w.text)
CREATE TEXT INDEX edges_by_server FOR ()-[conn:FOLLOWED_BY]->() ON (conn.server)